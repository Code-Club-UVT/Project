# Changelog

## Step 1: the gateway
- Created the `Gateway` project (YARP 2.3.0, EF Core read-only models, reference to `gateway-auth`)
- Routes and clusters built from the `Projects` table and refreshed by a poller; new, changed and deactivated projects take effect without a restart
- Edge token validation through `gateway-auth`; `ProjectAccessMiddleware` (access check, token exchange with core-api, header clean-up, correlation id); stale-while-error cache for the database reads
- Rate limiting (per user, per IP for core's auth endpoints), CORS, `/health` (database and routes loaded)
- `Dockerfile` (repo root as context) and `Dockerfile.dockerignore`
- Found and fixed while testing: YARP rejects the reserved policy names `default`/`disable` (renamed); the gateway answered 401 to everything just after start (readiness check added)
- Verified with a scripted run against the real core-api, PostgreSQL and a throwaway downstream API: 51 checks, all passed (see [implementation.md](implementation.md))
- Added `CLAUDE.md`, this `docs/` folder, a `.gitignore` and a row in the root `CLAUDE.md`

## Step 2: fixes from the code review
- A timeout of the call to core-api now gives 503 `Gateway.Unavailable` (it escaped as a 500 because the catch excluded every `OperationCanceledException`); `TtlCache` uses the same rule
- One unroutable project no longer blocks route updates for the others: projects are built and validated one by one (`TryBuild`, YARP `IConfigValidator`), invalid ones are skipped with a warning. The core auth route is now `core/auth`, so a project with the slug `core-auth` does not collide with it
- Verified (21 checks, all passed, throwaway sample API deleted): the 503 on a slow core-api (stub answering after 8 s with a 1 s timeout), a bad row (`BaseUrl` not a URL) plus a `core-auth` project plus a later project all handled while billing and core keep working, and the `JwksKeyStore` retry (see gateway-auth). The earlier 51-check run was not repeated in full

## Deployment support

- In the deployment its configuration is a JSON file mounted as `appsettings.Production.json` (see `~/codeclub-uvt/README.md` on the server).
- Forwarded headers: the client address is taken from `X-Forwarded-For` only when the request comes from a trusted proxy (`Proxy:TrustedNetworks`, default `172.16.0.0/12`), so rate limiting and the forwarded address use the real client IP behind Caddy. See the server README.

## Step 3: service split
- Tokens come from identity-api: `CoreApiOptions`/`CoreTokenClient` became `IdentityOptions`/`IdentityTokenClient` (settings section `Identity`), issuer `identity`, JWKS from identity
- Read-only models now map `core.Projects`, `core.ProjectMembers` and `identity.Users`
- The stricter auth rate limit moved from `/api/core/auth/*` to `/api/identity/auth/*` (route id `identity/auth`); core is now an `Exchange` project
- Library renamed from `gateway-auth` to `platform-kit` (settings section `PlatformAuth`); `Gateway.http` added
- Verified on the dev server: routes for the seeded and new projects, PassThrough for identity, Exchange for core and mock (project token swapped in; a token minted for one project is rejected by another project's API), access changes after the cache time, the per-IP limit on `/api/identity/auth`, and nothing under `/internal` or `/.well-known` reachable
