# Current implementation

## What it is

A class library (`GatewayAuth`, net10.0, framework reference `Microsoft.AspNetCore.App`, one package: `Microsoft.AspNetCore.Authentication.JwtBearer`). Every API behind the gateway references it to:

1. validate the **project token** the gateway forwards (issued by the core API),
2. read who is calling through `IRequestContext`,
3. share a correlation id across APIs,
4. expose the same `/health` endpoint.

The token flow itself (edge token, project tokens, `/internal/project-tokens`, JWKS) is described in `core-api/docs/auth-design.md`.

## Layout

```
gateway-auth/
  GatewayAuth.csproj
  Options/GatewayAuthOptions.cs       the GatewayAuth settings
  Authentication/JwksKeyStore.cs      downloads and caches the public keys (internal)
  RequestContext/
    IRequestContext.cs                public interface
    HttpRequestContext.cs             implementation reading HttpContext (internal)
    RequestContextMiddleware.cs       correlation id + log scope (internal)
    CorrelationIdHandler.cs           forwards the id on outgoing HttpClient calls (internal)
  Extensions/GatewayAuthExtensions.cs the public entry points
  docs/
```

## Using it in an API

```csharp
using GatewayAuth;
using GatewayAuth.RequestContext;

builder.Services.AddGatewayAuthentication(builder.Configuration);
builder.Services.AddRequestContext();
builder.Services.AddGatewayHealth();          // chain more health checks on the returned builder

app.UseAuthentication();
app.UseRequestContext();                      // after UseAuthentication, so the user is in the log scope
app.UseAuthorization();

app.MapGatewayHealth();                       // anonymous GET /health
```

Controllers then use the normal attributes (`[Authorize]`, `[Authorize(Roles = "Editor")]`, `[AllowAnonymous]`) and inject `IRequestContext`. For outgoing calls: `services.AddHttpClient("x").AddCorrelationIdPropagation();`.

## Configuration (`GatewayAuth` section)

| Key | Meaning |
|---|---|
| `Issuer` | Must equal the tokens' `iss` (core API `Jwt:Issuer`, currently `CoreApi`) |
| `Audience` | This API's project slug. Only tokens whose `aud` is exactly this are accepted |
| `JwksUrl` | Where the public keys are, for example `http://core-api:8080/.well-known/jwks.json`. Must be https unless `AllowInsecureJwksUrl` is true |
| `AllowInsecureJwksUrl` | Allows http for local development or a private Docker network. Default false |
| `KeysRefreshMinutes` | Keys are downloaded again after this long (default 60) |
| `UnknownKeyRefreshMinutes` | Minimum time between key downloads (default 1); see below |

Options are validated at startup. Environment variables use `__` (`GatewayAuth__Audience=billing`).

## Behaviour

- **Token validation**: ES256 only, issuer, audience, lifetime (30 seconds of clock skew), signature. Claims are not renamed (`MapInboundClaims=false`); `sub` is the name and `role` the role, so `[Authorize(Roles = "...")]` works. Every endpoint requires a valid token unless it has `[AllowAnonymous]` (fallback policy).
- **Keys** (`JwksKeyStore`): downloaded on the first token, then every `KeysRefreshMinutes`. A token with an unknown `kid` (the core API rotated its key) triggers an immediate download, so that same request is accepted. After a successful download the next one waits at least `UnknownKeyRefreshMinutes`, so tokens with invented key ids cannot make the API hammer the core API. After a failed download (core API not up yet, network blip) the next try is allowed after 5 seconds, so the API recovers shortly after the core API does; the previous keys stay in use meanwhile. The validator asks for keys synchronously, so the request that triggers a download is blocked for up to 5 seconds (the HTTP timeout). Other requests do not wait for it, except right after start while no key has been loaded at all (they wait up to 10 seconds). The timestamps are kept as `long` ticks with `Volatile` access (a `DateTimeOffset` is not read atomically).
- **Request context**: `IRequestContext` exposes `IsAuthenticated`, `UserId` (`sub`), `Username`, `Role` (`role`, the user's role in this project), `ProjectSlug` (= `Audience`), `CorrelationId`, `ClientIp`.
- **Correlation id**: `X-Correlation-Id` from the request is kept if it is 1 to 64 characters of letters, digits, `-`, `_`, `.`, `:`; otherwise a new 32-character id is generated. It is returned on the response and added, with the user id and project, to the log scope of the request.
- **Client IP**: the socket's remote address. Behind the gateway that is the gateway; add `UseForwardedHeaders` in the API (trusting the gateway) to see the real client.

## Verified

Against the real core-api and PostgreSQL, with a throwaway sample API that used the library (26 checks, all passed; the sample was deleted afterwards):

- valid project token accepted, with the correct request context; role-based authorization (`Viewer` gets 403 on an `Editor` endpoint, `Editor` gets 200)
- rejected: no token, the edge token (audience `gateway`), another project's token (audience), a tampered signature, edited claims (role escalation), garbage, an expired token
- key rotation: after the core API restarted with a new key, a token with the unknown key id was rejected while downloads were rate limited, and accepted on the first request once the interval had passed; the API keeps validating with cached keys while the core API is down
- an http `JwksUrl` without `AllowInsecureJwksUrl` stops the app at startup
- correlation id: kept when valid, replaced when unsafe, returned on the response, sent on outgoing `HttpClient` calls; `/health` is anonymous

Not covered: https JWKS with a real certificate, behaviour behind the real gateway, concurrency under load.
