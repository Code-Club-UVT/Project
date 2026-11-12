# Decisions

| Decision | Reason |
|---|---|
| Shared class library referenced by project reference | All APIs live in this repo; no package feed needed. NuGet later if APIs move out (see backlog) |
| Only the extension methods and `IRequestContext` are public | Everything else can change without touching the APIs |
| Tokens are validated with ES256 keys downloaded from the core API's JWKS | APIs can validate but not create tokens; key rotation needs no redeploy |
| Audience = the API's slug, checked by the standard JWT audience validation | Built in and on by default in every JWT library, so a missed check cannot let another project's token (or the edge token) in |
| Own `JwksKeyStore` instead of the stock `ConfigurationManager` | The stock manager refreshes in the background, so the first request after a key rotation still failed. The store downloads synchronously on an unknown key id, rate limited, and keeps the last good keys |
| Downloads are rate limited by one interval (`UnknownKeyRefreshMinutes`) for both unknown keys and periodic refresh | A flood of tokens with random key ids must not turn into a flood of requests to the core API |
| A synchronous download inside the validator callback | The IdentityModel key resolver is synchronous; the download is rare and has a 5 second timeout |
| https required for `JwksUrl` unless `AllowInsecureJwksUrl` is set | Keys fetched over plain http could be swapped by someone on the network; Docker-internal and local use opt in explicitly |
| Fallback policy requires an authenticated user | A forgotten attribute cannot expose an endpoint (same rule as core-api) |
| Claims are not renamed; `sub` is the name, `role` the role | Matches core-api's `TokenService` and its validation |
| Correlation id taken from the request only if it is short and plain, else generated | The value goes into logs and a response header |
| `ClientIp` is the socket address, not `X-Forwarded-For` | A forwarded header can be spoofed if the API is ever reachable directly; each API opts in with `UseForwardedHeaders` and a trusted proxy |
| Request context is scoped and reads `HttpContext` through `IHttpContextAccessor` | Available anywhere in a request without passing values around |
| Health is a one-line helper (`/health`, anonymous) | Same endpoint in every API for the gateway's probes |
| No NuGet package beyond `Microsoft.AspNetCore.Authentication.JwtBearer` | It already brings `Microsoft.IdentityModel.*` |
| A failed key download is retried after 5 seconds; only a successful one starts the longer `UnknownKeyRefreshMinutes` wait | Otherwise an API that started before the core API rejected every token for a full minute after the core API came up (found by code review) |
| A request that finds a download already running does not wait for it, unless no key is loaded yet | Many concurrent requests must not each block a thread for the HTTP timeout during a rotation or an outage |
| Renamed `gateway-auth` to `platform-kit` (`PlatformKit`) when the result and web plumbing moved in | It is no longer only about the gateway's tokens; it is the kit every service is built on. "Common" was avoided because it tends to become a dumping ground |
| `Result`, `Error`, `PagedResult`, `ResultControllerBase` and the exception handler live here | Identity, core and email all use the same Result pattern and ProblemDetails shape; one copy keeps the error format identical across services |
| Service-to-service calls use a key per caller (`InternalAuth:Callers`), not one shared key | A leaked key exposes one edge (for example identity to email), not every internal endpoint; the caller's name is known to the receiver |
| The internal scheme compares every configured key, without stopping at the first match | Timing does not tell which caller (or whether any) matched |
| `AddInternalClient` builds the HttpClient for internal calls (key header, correlation id, timeout) | Each caller would otherwise repeat the same few lines and the placeholder check |
| Settings section `PlatformAuth` (was `GatewayAuth`) | Matches the new name; services were reconfigured in the same change |
