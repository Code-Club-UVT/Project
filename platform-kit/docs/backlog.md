# Backlog and open points

## Open decisions
- **Permissions**: roles only for now. If permissions come back, add a `permission` claim and a policy helper here.
- **Packaging**: publish as a NuGet package (private feed or GitHub Packages) if services leave this repo.
- **Startup prefetch of the keys**: the first token triggers the first download. A hosted service could download at startup so the first request is not slower, and so a wrong `JwksUrl` shows in the log immediately.
- **Async key download**: the validator callback is synchronous, so a download blocks one request. Fine while rare; revisit if identity is often slow.
- **Real client IP**: document or wrap a `UseForwardedHeaders` setup trusting the gateway.
- **Internal key rotation**: `InternalAuth:Callers` takes one key per caller. To rotate without downtime, allow a list per caller (old and new) for a while.
- **Idempotency helper**: identity sends `Idempotency-Key` on outbox deliveries; receivers are idempotent by design today. A shared helper (a table of seen keys) would help the next receiver that is not naturally idempotent.
- **Tests**: no test project. The behaviours listed in `implementation.md` were checked by hand; an automated test project would keep them checked.

## TODO: audit log of important actions (discussed 2026-11-08, nothing decided yet)

Talk about it again before building. Notes from the first discussion:

- **What to log**: security events (login, failed login, logout, refresh, password reset, role or membership changes), admin changes (project create, update, delete, URL changes) and, with care for volume, gateway denials (`Project.NotMember`, invalid token). Not ordinary reads: those belong in application logs.
- **Where to store**: leaning to PostgreSQL, schema `audit` (append-only, relational queries by who, what and when; partition by month if it grows). A log stack (Loki, Elasticsearch) is better for free text and big volume but is one more system to run.
- **Who writes**: leaning to a small separate `audit-api` that owns the `audit` schema, plus a client in this library (`IAuditLog.Write(...)`) that posts to its internal endpoint through an outbox or bounded queue, so a slow audit service never blocks a request. This keeps "only the owner writes" and matches identity's outbox to email. The alternative, every API writing to the schema itself, is simpler but breaks that rule.
- **Event shape**: `Id, Timestamp, Service, Action, ActorUserId, ProjectId?, TargetType, TargetId, Outcome, CorrelationId, Ip, Details (jsonb)`. Never store passwords, tokens or whole request bodies.
- **Logs app**: later, a frontend app in the harness per API, behind a role. The `audit-api` would need a read endpoint scoped by project.
- **Open questions**: who may read the log (platform admins only, or project admins for their own project), retention period, and whether the gateway's denials are worth the volume.

## TODO: publish each API's OpenAPI schema (for the planned APIM-like app)

- Goal: every project API serves its OpenAPI JSON (the schema only, no Swagger UI) anonymously, and the gateway makes it public at `/api/<slug>/openapi/v1.json` in every environment. The APIM app reads the list of APIs (slug, name) from core-api's project list and the schema from that URL.
- Suggested shape: a helper here, `MapPlatformOpenApi()`, that maps `/api/openapi/v1.json` with `AllowAnonymous()`, plus a document transformer so every API documents itself the same way (see core-api/docs/backlog.md for the path convention). This needs the `Microsoft.AspNetCore.OpenApi` package in this library: ask before adding it.
- Open decision: public in prod too (reveals the API surface) or only behind a token/IP rule for the APIM app. Decide before building.
