using PlatformKit.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace PlatformKit.RequestContext;

internal sealed class HttpRequestContext(IHttpContextAccessor accessor, IOptions<PlatformAuthOptions> options) : IRequestContext
{
    internal const string CorrelationIdItem = "PlatformKit.CorrelationId";

    private HttpContext? Http => accessor.HttpContext;

    public bool IsAuthenticated => Http?.User.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Http?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Username => Http?.User.FindFirst("username")?.Value;

    public string? Role => Http?.User.FindFirst("role")?.Value;

    public string ProjectSlug => options.Value.Audience;

    public string CorrelationId =>
        Http?.Items[CorrelationIdItem] as string
        ?? Http?.TraceIdentifier
        ?? string.Empty;

    public string? ClientIp => Http?.Connection.RemoteIpAddress?.ToString();
}
