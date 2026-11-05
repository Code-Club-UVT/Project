using Gateway.Access;
using Gateway.Routing;
using Gateway.Tokens;
using PlatformKit.RequestContext;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Model;

namespace Gateway.Pipeline;

/// <summary>
/// Runs for every proxied request, after the edge token was validated and before the request is forwarded.
/// <list type="bullet">
/// <item><b>Exchange</b> projects: the user must be active and a member of the project; the <c>Authorization</c> header is
/// replaced by a project token from the core API.</item>
/// <item><b>PassThrough</b> projects (core): the edge token is forwarded unchanged; no token is fine (login, register), but
/// a token that is present and invalid is rejected here.</item>
/// </list>
/// Headers a client could use to pretend to be someone else are removed first.
/// </summary>
public sealed class ProjectAccessMiddleware(RequestDelegate next, ILogger<ProjectAccessMiddleware> logger)
{
    private const string UserHeaderPrefix = "X-User-";
    private const string InternalKeyHeader = "X-Internal-Key";

    public async Task InvokeAsync(
        HttpContext context,
        IProjectAccessService access,
        IProjectTokenProvider tokens,
        IRequestContext requestContext)
    {
        var metadata = context.GetReverseProxyFeature().Route.Config.Metadata;
        if (metadata is null
            || !metadata.TryGetValue(ProxyConfigBuilder.SlugKey, out var slug)
            || !metadata.TryGetValue(ProxyConfigBuilder.TokenModeKey, out var mode)
            || !metadata.TryGetValue(ProxyConfigBuilder.ProjectIdKey, out var projectIdText)
            || !Guid.TryParse(projectIdText, out var projectId))
        {
            logger.LogError("Route {RouteId} has no project metadata", context.GetReverseProxyFeature().Route.Config.RouteId);
            await GatewayProblems.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "Gateway.Misconfigured", "The route is not configured correctly.");
            return;
        }

        RemoveClientIdentityHeaders(context.Request.Headers);
        context.Request.Headers[RequestContextHeaders.CorrelationId] = requestContext.CorrelationId;

        var authenticated = context.User.Identity?.IsAuthenticated == true;

        if (mode == ProxyConfigBuilder.PassThrough)
        {
            if (!authenticated && context.Request.Headers.ContainsKey(HeaderNames.Authorization))
            {
                await GatewayProblems.WriteAsync(context, StatusCodes.Status401Unauthorized, "Auth.InvalidToken", "The access token is invalid or has expired.");
                return;
            }

            await next(context);
            return;
        }

        if (!authenticated || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
        {
            await GatewayProblems.WriteAsync(context, StatusCodes.Status401Unauthorized, "Auth.InvalidToken", "The access token is invalid or has expired.");
            return;
        }

        string projectToken;
        try
        {
            var cancellationToken = context.RequestAborted;

            var decision = await access.GetAsync(userId, projectId, cancellationToken);
            if (!decision.UserExists || !decision.UserActive)
            {
                await GatewayProblems.WriteAsync(context, StatusCodes.Status401Unauthorized, "Auth.AccountUnavailable", "The account does not exist or is disabled.");
                return;
            }

            if (decision.RoleId is not { } roleId)
            {
                await GatewayProblems.WriteAsync(context, StatusCodes.Status403Forbidden, "Project.NotMember", "You do not have access to this project.");
                return;
            }

            var token = await tokens.GetAsync(userId, slug, roleId, cancellationToken);
            switch (token.Outcome)
            {
                case TokenOutcome.NotMember:
                    await GatewayProblems.WriteAsync(context, StatusCodes.Status403Forbidden, "Project.NotMember", "You do not have access to this project.");
                    return;
                case TokenOutcome.NotFound:
                    await GatewayProblems.WriteAsync(context, StatusCodes.Status404NotFound, "Project.NotFound", "The project was not found.");
                    return;
            }

            projectToken = token.Token!;
        }
        // A timeout of the call to core-api is also an OperationCanceledException; only a client that went away is not ours to answer.
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not check access or get a project token for project {Slug}", slug);
            await GatewayProblems.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "Gateway.Unavailable", "The gateway cannot check access right now. Try again shortly.");
            return;
        }

        context.Request.Headers[HeaderNames.Authorization] = "Bearer " + projectToken;
        await next(context);
    }

    private static void RemoveClientIdentityHeaders(IHeaderDictionary headers)
    {
        headers.Remove(InternalKeyHeader);
        foreach (var name in headers.Keys.Where(k => k.StartsWith(UserHeaderPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            headers.Remove(name);
        }
    }
}

internal static class RequestContextHeaders
{
    public const string CorrelationId = "X-Correlation-Id";
}
