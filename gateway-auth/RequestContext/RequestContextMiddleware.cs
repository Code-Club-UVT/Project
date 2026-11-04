using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GatewayAuth.RequestContext;

/// <summary>
/// Settles the correlation id (incoming header if it looks safe, otherwise a new one), returns it on the response, and
/// puts it with the user id and project in the log scope. Put it after <c>UseAuthentication()</c> so the user is known.
/// </summary>
internal sealed class RequestContextMiddleware(RequestDelegate next, ILogger<RequestContextMiddleware> logger)
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    private const int MaxCorrelationIdLength = 64;

    public async Task InvokeAsync(HttpContext httpContext, IRequestContext requestContext)
    {
        var correlationId = ReadOrCreateCorrelationId(httpContext);
        httpContext.Items[HttpRequestContext.CorrelationIdItem] = correlationId;
        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["UserId"] = requestContext.UserId,
            ["Project"] = requestContext.ProjectSlug
        }))
        {
            await next(httpContext);
        }
    }

    private static string ReadOrCreateCorrelationId(HttpContext httpContext)
    {
        var incoming = httpContext.Request.Headers[CorrelationIdHeader].ToString();

        // The value ends up in logs and response headers, so only accept a short, plain identifier.
        return IsSafe(incoming) ? incoming : Guid.NewGuid().ToString("N");
    }

    private static bool IsSafe(string value)
    {
        if (value.Length is 0 or > MaxCorrelationIdLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }
}
