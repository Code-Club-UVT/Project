# Decisions

| Decision | Reason |
|---|---|
| YARP as the proxy, with routes and clusters built in code from the database (`IProxyConfigProvider`) | Adding a project must not need a redeploy; YARP's config files are static |
| Poll the `Projects` table every 30 s instead of push notifications | Simple and robust across processes; a change shows within one interval. LISTEN/NOTIFY is in the backlog |
| The gateway reads the core API's tables with its own read-only models | Core-api owns the data and the rules; the gateway needs only fast reads. Only the needed columns are mapped, so core-api can add columns freely. No migrations here |
| Core-api mints project tokens; the gateway asks through `POST /internal/project-tokens` and caches the result | One signer and one set of rules (core-api re-checks membership). The call happens about once per token lifetime per user and project |
| The gateway checks access itself first (cached DB read), then asks for the token | Cheap rejection of non-members without calling core-api, and the role id in the token cache key makes a role change take effect |
| Public path `/api/<slug>`; the prefix is removed; `BaseUrl` may carry a path | No path column or strip flag; core-api's own `/api/...` routes stay unchanged |
| Core is `PassThrough`: anonymous at the route level, token forwarded unchanged, but a present-and-invalid token is refused by the gateway | Core-api has anonymous endpoints (login) and validates its own edge token; refusing bad tokens early keeps the gateway the place where tokens are checked |
| Exchange routes use the `default` authorization policy (authenticated user) | A missing or invalid token never reaches the access check |
| The edge token is validated with `platform-kit` (audience `gateway`) | Same validation code and key handling as every API, including key rotation |
| Stale-while-error cache (`TtlCache`): fresh 30 s, usable up to 5 minutes if the database fails, then 503 | Short outages do not take every API down, but access is never granted on data older than the grace period (fail closed) |
| Cache stampede protection with striped locks | Many parallel requests of one user do not each hit the database or core-api |
| Project tokens are cached until a quarter of their life (max 60 s) before they expire | A downstream API never receives a token that is about to expire |
| Strip `X-User-*`, `X-Internal-Key`, client `Forwarded`; set `X-Forwarded-*` ourselves | The gateway is the first hop; nothing a client sends may look like identity or internal traffic |
| Correlation id from the request if safe, else generated; sent downstream and returned | One id per request across the gateway and APIs (`platform-kit` request context) |
| Rate limiting here, not in core-api: `per-ip-auth` (10/min per IP) for core's auth endpoints, `per-user` (300/min per user, IP if anonymous) for the rest | One place for limits; the same numbers core-api used for auth, a generous default for the rest |
| Policy names `per-user`, `per-ip-auth` and `frontend` | YARP reserves `default` and `disable` for CORS and rate limiting (found by testing: the whole route config was rejected) |
| CORS only at the gateway, for `Cors:AllowedOrigins`, with credentials | The browser talks to one origin; APIs behind it do not need CORS |
| `/health` = database reachable and routes loaded | A probe must not send traffic to a gateway that answers 401 to everything because it has no routes yet (found by testing) |
| Active health checks only for projects that have a `HealthUrl` | Opt-in per project; `Destination.Health` is the full URL so a base path (core's `/api`) does not matter |
| Gateway Dockerfile builds from the repo root | It references `../platform-kit`; `Dockerfile.dockerignore` limits the context to the two folders |
| In-memory caches and rate limiters | Fine for one instance; several instances would each have their own (backlog) |
| Each project is built and validated on its own (`ProxyConfigBuilder.TryBuild` plus YARP's `IConfigValidator`); an invalid one is skipped with a warning | YARP rejects the whole configuration if one route or cluster is invalid, and the poller would not retry, so one bad row silently froze all route updates (found by code review) |
| The extra core route has the id `core/auth` | A slug cannot contain `/`, so a project can never get the same route id (a project with the slug `core-auth` did) |
| Exceptions in the access check are answered with 503 unless the client itself aborted (`RequestAborted`) | The timeout of the call to core-api is also an `OperationCanceledException`; excluding all of them turned a slow core-api into a 500 (found by code review) |
| After the service split: tokens come from identity (`Identity:*` settings, `IdentityTokenClient`), the auth rate-limit route is `/api/identity/auth`, and the read-only models are schema-qualified (`core.Projects`, `core.ProjectMembers`, `identity.Users`) | Identity is the only signer; each service owns a schema, the gateway keeps reading only what it needs |
| Core is an ordinary `Exchange` project; identity is the only `PassThrough` one | Core no longer validates edge tokens; identity must accept anonymous login and register |
