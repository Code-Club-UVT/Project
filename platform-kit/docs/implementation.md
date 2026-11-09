# Current implementation

## What it is

A class library (`PlatformKit`, net10.0, framework reference `Microsoft.AspNetCore.App`, one package: `Microsoft.AspNetCore.Authentication.JwtBearer`). It was `gateway-auth` (auth and request context only) and was renamed when the result and web plumbing moved in. Every service references it to:

1. validate the **project token** the gateway forwards (issued by identity), or, in the gateway, the edge token;
2. read who is calling through `IRequestContext`, and share a correlation id across services;
3. return expected failures as `Result` and turn them into ProblemDetails the same way everywhere;
4. protect `/internal/...` endpoints with a per-caller key, and call other services' internal endpoints;
5. expose the same `/health` endpoint.

The token flow itself (edge token, project tokens, `/internal/project-tokens`, JWKS) is described in `identity-api/docs/auth-design.md`.

## Layout

```
platform-kit/
  PlatformKit.csproj
  Auth/
    PlatformAuthOptions.cs              the PlatformAuth settings
    JwksKeyStore.cs                     downloads and caches the public keys (internal)
  RequestContext/
    IRequestContext.cs                  public interface
    HttpRequestContext.cs               implementation reading HttpContext (internal)
    RequestContextMiddleware.cs         correlation id + log scope (internal)
    CorrelationIdHandler.cs             forwards the id on outgoing HttpClient calls (internal)
  Results/                              Result, Result<T>, Error, ErrorType, PagedResult<T>
  Web/
    ResultControllerBase.cs             Result -> 200/204 or ProblemDetails with "code" (public, abstract)
    GlobalExceptionHandler.cs           unhandled exception -> 500 ProblemDetails (internal)
  Internal/
    InternalAuth.cs                     scheme name "InternalKey", header "X-Internal-Key"
    InternalAuthOptions.cs              InternalAuth:Callers (caller name -> key)
    InternalKeyAuthenticationHandler.cs the scheme (internal)
    InternalKeyHandler.cs               adds the key to outgoing calls (internal)
  Extensions/
    PlatformAuthExtensions.cs           AddPlatformAuthentication, AddRequestContext, UseRequestContext,
                                        AddCorrelationIdPropagation, AddPlatformHealth, MapPlatformHealth
    PlatformWebExtensions.cs            AddPlatformProblemDetails
    InternalAuthExtensions.cs           AddInternalKeyAuthentication, AddInternalClient
  docs/
```

## Using it in a service

```csharp
using PlatformKit;

builder.Services.AddPlatformAuthentication(builder.Configuration);              // project tokens (default scheme)
builder.Services.AddAuthentication().AddInternalKeyAuthentication(builder.Environment); // only if it has /internal endpoints
builder.Services.AddRequestContext();
builder.Services.AddPlatformProblemDetails();
builder.Services.AddPlatformHealth();          // chain more health checks on the returned builder

app.UseExceptionHandler();
app.UseAuthentication();
app.UseRequestContext();                      // after UseAuthentication, so the user is in the log scope
app.UseAuthorization();

app.MapControllers();
app.MapPlatformHealth();                      // anonymous GET /health
```

Controllers derive from `ResultControllerBase` (add the route yourself, for example `[Route("api/[controller]")]`), return `ToActionResult(result)`, use the normal attributes (`[Authorize]`, `[Authorize(Roles = "Editor")]`, `[AllowAnonymous]`) and inject `IRequestContext`. Internal controllers add `[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]` and live under `/internal/...`, which the gateway never routes. For outgoing calls: `services.AddHttpClient("x").AddCorrelationIdPropagation();`, or `services.AddInternalClient(name, baseUrl, key, timeout, environment)` for another service's internal endpoints.

A service without project tokens (email-api) makes the internal scheme its default: `services.AddAuthentication(InternalAuth.SchemeName).AddInternalKeyAuthentication(env)` plus a fallback policy on that scheme.

## Configuration: `PlatformAuth` section (token validation)

| Key | Meaning |
|---|---|
| `Issuer` | Must equal the tokens' `iss` (identity `Jwt:Issuer`, currently `identity`) |
| `Audience` | This API's project slug. Only tokens whose `aud` is exactly this are accepted |
| `JwksUrl` | Where the public keys are, for example `http://core-api:8080/.well-known/jwks.json`. Must be https unless `AllowInsecureJwksUrl` is true |
| `AllowInsecureJwksUrl` | Allows http for local development or a private Docker network. Default false |
| `KeysRefreshMinutes` | Keys are downloaded again after this long (default 60) |
| `UnknownKeyRefreshMinutes` | Minimum time between key downloads (default 1); see below |

Options are validated at startup. Environment variables use `__` (`PlatformAuth__Audience=billing`).

## Behaviour

- **Token validation**: ES256 only, issuer, audience, lifetime (30 seconds of clock skew), signature. Claims are not renamed (`MapInboundClaims=false`); `sub` is the name and `role` the role, so `[Authorize(Roles = "...")]` works. Every endpoint requires a valid token unless it has `[AllowAnonymous]` (fallback policy).
- **Keys** (`JwksKeyStore`): downloaded on the first token, then every `KeysRefreshMinutes`. A token with an unknown `kid` (identity rotated its key) triggers an immediate download, so that same request is accepted. After a successful download the next one waits at least `UnknownKeyRefreshMinutes`, so tokens with invented key ids cannot make the API hammer identity. After a failed download (identity not up yet, network blip) the next try is allowed after 5 seconds, so the API recovers shortly after identity does; the previous keys stay in use meanwhile. The validator asks for keys synchronously, so the request that triggers a download is blocked for up to 5 seconds (the HTTP timeout). Other requests do not wait for it, except right after start while no key has been loaded at all (they wait up to 10 seconds). The timestamps are kept as `long` ticks with `Volatile` access (a `DateTimeOffset` is not read atomically).
- **Request context**: `IRequestContext` exposes `IsAuthenticated`, `UserId` (`sub`), `Username`, `Role` (`role`, the user's role in this project), `ProjectSlug` (= `Audience`), `CorrelationId`, `ClientIp`.
- **Correlation id**: `X-Correlation-Id` from the request is kept if it is 1 to 64 characters of letters, digits, `-`, `_`, `.`, `:`; otherwise a new 32-character id is generated. It is returned on the response and added, with the user id and project, to the log scope of the request.
- **Client IP**: the socket's remote address. Behind the gateway that is the gateway; add `UseForwardedHeaders` in the API (trusting the gateway) to see the real client.


## Configuration: `InternalAuth` section (service-to-service)

| Key | Meaning |
|---|---|
| `Callers:<name>` | The key that caller sends in `X-Internal-Key`. The matching name becomes the `sub` claim (for logs and records). At least one caller; every key at least 32 characters; outside Development no key may start with `CHANGE-ME` |

Example: identity has `InternalAuth:Callers:gateway`; core-api and email-api have `InternalAuth:Callers:identity`. Every key is compared (SHA-256, constant time, no early exit), so timing reveals neither the key nor which caller matched. `AddInternalClient` refuses a missing key, or a `CHANGE-ME` key outside Development, at startup.

## Results and errors

- `Result` / `Result<T>` for expected failures; `Error(Code, Message, Type)` with `ErrorType` Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409, TooManyRequests 429. `ResultControllerBase` gives 200 with the value, 204 without one, and a ProblemDetails (RFC 9457) with the `code` extension for failures.
- `GlobalExceptionHandler`: logs the exception and returns a 500 ProblemDetails; the message is included only in Development. A request the client aborted is not reported.

## Verified

Against the real core-api and PostgreSQL, with a throwaway sample API that used the library (26 checks, all passed; the sample was deleted afterwards):

- valid project token accepted, with the correct request context; role-based authorization (`Viewer` gets 403 on an `Editor` endpoint, `Editor` gets 200)
- rejected: no token, the edge token (audience `gateway`), another project's token (audience), a tampered signature, edited claims (role escalation), garbage, an expired token
- key rotation: after the core API (now identity) restarted with a new key, a token with the unknown key id was rejected while downloads were rate limited, and accepted on the first request once the interval had passed; the API keeps validating with cached keys while it is down
- an http `JwksUrl` without `AllowInsecureJwksUrl` stops the app at startup
- correlation id: kept when valid, replaced when unsafe, returned on the response, sent on outgoing `HttpClient` calls; `/health` is anonymous

Not covered: https JWKS with a real certificate, behaviour behind the real gateway, concurrency under load.
