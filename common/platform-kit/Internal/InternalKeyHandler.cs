namespace PlatformKit.Internal;

/// <summary>Adds the <c>X-Internal-Key</c> header to every outgoing call of an internal client.</summary>
internal sealed class InternalKeyHandler(string key) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(InternalAuth.HeaderName);
        request.Headers.TryAddWithoutValidation(InternalAuth.HeaderName, key);

        return base.SendAsync(request, cancellationToken);
    }
}
