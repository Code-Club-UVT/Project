# Backlog and open points

## Next steps
- Docker compose for everything (PostgreSQL, core-api, gateway, project APIs) on a private network where only the gateway publishes a port. Core-api needs its own `Dockerfile` (listed in `core-api/docs/backlog.md`).
- A SELECT-only database user for the gateway, and its connection string (`ConnectionStrings__PostgresConnection`) in the deployment.
- Point the SPA at the gateway (`/api/core/...`) and set `Cors__AllowedOrigins`.

## Open decisions
- **Real client IP**: behind another reverse proxy (nginx, a load balancer) the gateway sees the proxy as the client, which breaks the per-IP rate limit. Add `UseForwardedHeaders` with the trusted proxy.
- **Several gateway instances**: caches and rate limiters are per instance. Use a distributed cache / shared limiter if it scales out.
- **Push instead of poll**: PostgreSQL LISTEN/NOTIFY (or a version row) to update routes and drop cached access immediately.
- **Active health checks**: configured when a project has a `HealthUrl`, but not exercised by the tests (a destination being marked unhealthy and recovering). With a single destination YARP still tries it when it is unhealthy.
- **Unknown routes answer 401** for anonymous requests (the fallback policy of `gateway-auth`) and 404 for authenticated ones. Probably fine; revisit if the SPA needs to tell them apart.
- **Per-project settings**: timeouts, body size limits, rate limits, extra allowed origins. All global today.
- **Streaming, WebSockets, large uploads**: YARP supports them, nothing here was tested with them.
- **Monitoring**: a status endpoint or dashboard that shows project health (from the probes), request metrics and the cache state; OpenTelemetry or Prometheus.
- **Request logging / audit**: log denied requests (403/401) with user, project and reason in a queryable store (see core-api backlog).
- **Edge token revocation**: a disabled user is refused after the access cache time; a stolen edge token otherwise works until it expires (15 minutes).
- **Core path allow-list**: the whole `/api/core/**` is anonymous at the gateway; core-api decides per endpoint. Fine while core-api is the only one that knows its endpoints.
- **Tests**: no test project. The behaviour in `implementation.md` was verified with a scripted run; an automated integration test project would keep it verified.
- **Startup order**: routes load a moment after start; `/health` stays 503 until then. A probe-based start order in compose makes this invisible.
- **PassThrough only for core, enforced in the data**: today only `ProjectService` in core-api stops a non-system project from being `PassThrough`. A row written another way would make the gateway send users' edge tokens to its `BaseUrl`. Add a CHECK constraint (`TokenMode <> 'PassThrough' OR IsSystem`) in core-api and/or make the gateway honour `PassThrough` only for the `core` slug (open from the code review).
- **Access query**: `ProjectAccessService` runs two queries per cache miss (user, membership); one joined query would halve the load (open from the code review).
