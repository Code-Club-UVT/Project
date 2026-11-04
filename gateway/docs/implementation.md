# Current implementation

## What it is

A YARP reverse proxy (`Yarp.ReverseProxy` 2.3.0) on .NET 10. Every API is reached through it at `/api/<slug>/...`; core-api is the project `core`. Packages: `Yarp.ReverseProxy`, `Npgsql.EntityFrameworkCore.PostgreSQL` (read-only access), and the `gateway-auth` project reference (edge token validation, request context, health).

## Layout

```
gateway/
  Program.cs                     pipeline: CORS, authentication, request context, rate limiter, authorization, health, proxy
  Options/GatewayOptions.cs      CoreApiOptions, RoutingOptions, RateLimitOptions, FrontendCorsOptions
  Data/                          GatewayDbContext (read-only models of Projects, ProjectMembers, Users), health check
  Routing/
    ProxyConfigBuilder.cs        projects -> YARP routes and clusters (pure)
    DbProxyConfigProvider.cs     current config + change signal for YARP
    ProjectConfigPoller.cs       background service: reads the projects, updates the routes
    RoutesLoadedHealthCheck.cs   not ready until routes were loaded once
  Access/
    TtlCache.cs                  cache with a fresh time and a stale grace period
    ProjectAccessService.cs      user active? member? role? (cached)
  Tokens/
    CoreTokenClient.cs           POST {core}/internal/project-tokens with X-Internal-Key
    ProjectTokenProvider.cs      caches project tokens until shortly before they expire
  Pipeline/
    ProjectAccessMiddleware.cs   runs before forwarding: access check, token swap, header clean-up
    GatewayProblems.cs           ProblemDetails written by the gateway itself
  Extensions/GatewayServiceExtensions.cs   DI registration
  Dockerfile, Dockerfile.dockerignore
  docs/
```

## How a request flows

1. **CORS** for the origins in `Cors:AllowedOrigins` (credentials allowed, `X-Correlation-Id` exposed).
2. **Authentication**: `gateway-auth` validates the edge token (ES256, issuer `CoreApi`, audience `gateway`) with the keys from core-api's JWKS. A request without a token is still passed on; a token that is present but invalid is anonymous at this point.
3. **Request context**: the correlation id is taken from `X-Correlation-Id` (if safe) or generated, and returned on the response.
4. **Rate limiter**: policy `per-user` (the token's `sub`, else the client IP) for everything, `per-ip-auth` for `/api/core/auth/*` (login, register, password reset, ...).
5. **Authorization**: `Exchange` routes need a valid edge token (401 otherwise, empty body from the framework). `PassThrough` routes (core) are anonymous at this level.
6. **ProjectAccessMiddleware** (inside the YARP pipeline):
   - removes `X-User-*` and `X-Internal-Key` headers from the client and sets `X-Correlation-Id`;
   - **PassThrough** (core): a token that is present but invalid gets 401 `Auth.InvalidToken`; otherwise the request is forwarded with the original `Authorization` header (core-api validates it itself and decides about anonymous endpoints);
   - **Exchange**: reads the user's access from the database (cached): unknown or disabled user 401 `Auth.AccountUnavailable`, not a member 403 `Project.NotMember`; then gets a project token from core-api (cached) and replaces `Authorization` with it. If the database or core-api cannot be used and there is no usable cached value (this includes the call to core-api timing out after `CoreApi:TimeoutSeconds`): 503 `Gateway.Unavailable`.
7. **YARP** removes `/api/<slug>` from the path and forwards to the project's `BaseUrl` (which may contain a path: core is `http://host:5054/api`, so `/api/core/auth/login` becomes `/api/auth/login`). `X-Forwarded-*` headers are set by the gateway, a client `Forwarded` header is dropped.

## Routes from the database

`ProjectConfigPoller` reads the active projects every `Routing:PollSeconds` (30). For each project: route `/api/<slug>/{**rest}` and a cluster with one destination (`BaseUrl`). A project with a `HealthUrl` gets YARP active health checks (every 30 s, 5 s timeout, consecutive failures). The core project also gets a second route `/api/core/auth/{**rest}` with the stricter rate limit. The config is replaced only when something changed (fingerprint of id, slug, URLs, token mode, `UpdatedAt`). New, changed, deactivated and deleted projects take effect within one poll interval without a restart. If the database cannot be read the current routes stay. A project that cannot be routed (an invalid URL in its row, or anything YARP's own validator rejects) is skipped with a warning (`Project <slug> is not routed: ...`) and the other projects are updated normally; otherwise YARP would reject the whole configuration and no change would reach the routes. The extra core route is named `core/auth`, an id no slug can have, so it cannot collide with a project's route.

Readiness: `/health` is 200 only when PostgreSQL is reachable and the routes were loaded at least once (before the first load every request would be refused, so probes must not send traffic yet).

## Caching

| What | Fresh for | If the database / core-api fails |
|---|---|---|
| Routes | `Routing:PollSeconds` (30) | current routes stay |
| Access (user active, role) per user and project | `Routing:AccessCacheSeconds` (30) | the old entry is used for up to `Routing:StaleGraceSeconds` (300), then 503 |
| Project token per user, project and role | until a quarter of its life (at most 60 s) before it expires | a cached token keeps being used; a missing one means 503 |

So a revoked grant or a disabled user can keep working for up to about 30 seconds (plus the token's remaining life, 5 minutes, downstream: the project API trusts the token it gets). A role change gets a new token at once (the role id is part of the cache key) once the access entry refreshes. In-memory caches: with several gateway instances each has its own; that is fine for these lifetimes.

## Configuration

| Section | Keys |
|---|---|
| `ConnectionStrings:PostgresConnection` | the core API's database (read-only user in production) |
| `GatewayAuth` | `Issuer` (`CoreApi`), `Audience` (`gateway`), `JwksUrl`, `AllowInsecureJwksUrl` (see gateway-auth docs) |
| `CoreApi` | `InternalUrl` (root of core-api, no `/api`), `InternalKey` (same as core-api `Internal:ApiKey`; the `CHANGE-ME` placeholder works only in Development), `TimeoutSeconds` (5) |
| `Routing` | `PollSeconds` (30), `AccessCacheSeconds` (30), `StaleGraceSeconds` (300) |
| `RateLimiting` | `AuthPerMinute` (10, per IP), `DefaultPerMinute` (300, per user or IP) |
| `Cors` | `AllowedOrigins` (the SPA origins) |

Environment variables use `__` (`CoreApi__InternalKey`, `Cors__AllowedOrigins__0`). Options are validated at startup. Development values are in `appsettings.Development.json` (database on localhost, core-api at `http://localhost:5054`).

## Running and testing locally

1. `docker compose up -d` in `core-api/` (PostgreSQL), `dotnet ef database update`.
2. Start core-api on `http://localhost:5054` (the `core` project row is seeded with that address), then the gateway (`dotnet run` in `gateway/`, port 5100).
3. Point the SPA at `http://localhost:5100/api/core/...`; project APIs at `/api/<slug>/...`.

## Verified

Scripted run against the real core-api, PostgreSQL and a throwaway downstream API built on `gateway-auth` (51 checks, all passed; the sample was deleted):

- the SPA's view of core-api through the gateway: login, refresh cookie keeps `Path=/api/core/auth`, refresh, token forwarded unchanged, anonymous endpoints, invalid token rejected by the gateway, role checks, admin endpoints
- a project created through the gateway is routed within a poll interval; the downstream sees the user, the project role and the stripped path and query
- 401 without or with an invalid token, 403 for a non-member, downstream role check
- header handling: `X-User-*`, `X-Internal-Key`, client `X-Forwarded-For` and `Forwarded` do not reach the downstream API; it gets a project token (not the edge token); the correlation id is kept and passed on
- `/internal/project-tokens` cannot be reached through the gateway (plain and encoded `..` paths)
- CORS preflight and real calls for the allowed origin; no allow header for another origin
- role change, revoked access, disabled account, deactivated project and reactivation, all taking effect within the cache times
- PostgreSQL stopped: a user already seen is still served (stale entry), a new user gets 503, `/health` returns 503, routes survive; after the restart service resumes
- rate limits: auth endpoints per IP (429), other core endpoints unaffected, default limit per user, other users unaffected
- downstream API down: 502/503, gateway stays healthy; the image builds and refuses to start without configuration

Not covered: active health checks actually marking a destination unhealthy, WebSockets / streaming, large uploads, several gateway instances, https and a real proxy in front, behaviour under load.
