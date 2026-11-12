using System.Net;
using System.Net.Http.Json;
using Gateway.Options;
using Microsoft.Extensions.Options;
using PlatformKit.Internal;

namespace Gateway.Tokens;

public enum TokenOutcome
{
    Issued,
    NotMember,
    NotFound
}

public sealed record TokenResult(TokenOutcome Outcome, string? Token = null, DateTimeOffset ExpiresAt = default);

/// <summary>Asks identity for a project token (<c>POST /internal/project-tokens</c>, authenticated with the gateway's internal key).</summary>
public sealed class IdentityTokenClient(HttpClient httpClient)
{
    private sealed record Response(string Token, DateTime ExpiresAt);

    /// <exception cref="HttpRequestException">Identity answered something unexpected or could not be reached.</exception>
    public async Task<TokenResult> IssueAsync(Guid userId, string slug, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("internal/project-tokens", new { userId, slug }, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var body = await response.Content.ReadFromJsonAsync<Response>(cancellationToken)
                    ?? throw new HttpRequestException("Identity returned an empty token response.");
                return new TokenResult(TokenOutcome.Issued, body.Token, new DateTimeOffset(DateTime.SpecifyKind(body.ExpiresAt, DateTimeKind.Utc)));
            case HttpStatusCode.Forbidden:
                return new TokenResult(TokenOutcome.NotMember);
            case HttpStatusCode.NotFound:
                return new TokenResult(TokenOutcome.NotFound);
            default:
                throw new HttpRequestException($"Identity answered {(int)response.StatusCode} to a token request.");
        }
    }

    public static void Configure(HttpClient client, IdentityOptions options)
    {
        client.BaseAddress = new Uri(options.InternalUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Add(InternalAuth.HeaderName, options.InternalKey);
    }
}
