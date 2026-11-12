using Microsoft.AspNetCore.Http;

namespace PlatformKit.RequestContext;

/// <summary>Sends the current request's correlation id on outgoing calls, so one id follows a request across APIs.</summary>
internal sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestContext = accessor.HttpContext?.RequestServices.GetService(typeof(IRequestContext)) as IRequestContext;
        if (requestContext is not null && !request.Headers.Contains(RequestContextMiddleware.CorrelationIdHeader))
        {
            request.Headers.TryAddWithoutValidation(RequestContextMiddleware.CorrelationIdHeader, requestContext.CorrelationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
