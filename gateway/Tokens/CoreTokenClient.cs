using System.Net;
using System.Net.Http.Json;
using Gateway.Options;
using Microsoft.Extensions.Options;

namespace Gateway.Tokens;

public enum TokenOutcome
{
    Issued,
    NotMember,
    NotFound
}

public sealed record TokenResult(TokenOutcome Outcome, string? Token = null, DateTimeOffset ExpiresAt = default);

/// <summary>Asks the core API for a project token (<c>POST /internal/project-tokens</c>, authenticated with the shared key).</summary>
public sealed class CoreTokenClient(HttpClient httpClient)
{
    public const string InternalKeyHeader = "X-Internal-Key";

    private sealed record Response(string Token, DateTime ExpiresAt);

    /// <exception cref="HttpRequestException">The core API answered something unexpected or could not be reached.</exception>
    public async Task<TokenResult> IssueAsync(Guid userId, string slug, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync("internal/project-tokens", new { userId, slug }, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var body = await response.Content.ReadFromJsonAsync<Response>(cancellationToken)
                    ?? throw new HttpRequestException("The core API returned an empty token response.");
                return new TokenResult(TokenOutcome.Issued, body.Token, new DateTimeOffset(DateTime.SpecifyKind(body.ExpiresAt, DateTimeKind.Utc)));
            case HttpStatusCode.Forbidden:
                return new TokenResult(TokenOutcome.NotMember);
            case HttpStatusCode.NotFound:
                return new TokenResult(TokenOutcome.NotFound);
            default:
                throw new HttpRequestException($"The core API answered {(int)response.StatusCode} to a token request.");
        }
    }

    public static void Configure(HttpClient client, CoreApiOptions options)
    {
        client.BaseAddress = new Uri(options.InternalUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Add(InternalKeyHeader, options.InternalKey);
    }
}
