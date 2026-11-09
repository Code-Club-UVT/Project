# Backlog and open points

Account, token and email items moved to identity-api/docs/backlog.md and email-api/docs/backlog.md with the split.

## Open decisions
- **MongoDB and Neo4j**: which data goes in each, and the driver packages (`MongoDB.Driver`, `Neo4j.Driver`); each gets its own folder under `Data/` and its own connection string. Decide where their models live (`Entities/` is relational-only for now).
- **Duplicate project slug / role name race**: a simultaneous create hits the unique index and surfaces as a 500; map it to 409.
- **Concurrent grant**: two simultaneous grants of the same user to the same project give a 500 from the primary key instead of 409 `ProjectMember.AlreadyMember`. The same race in the default-role grant gives a 500 that identity's outbox retries (the second run finds the membership), so it heals by itself.
- **Duplicated helpers**: `CheckProjectAsync` is in both `ProjectRoleService` and `ProjectMemberService`, and `NullIfBlank` in several services; move them to a shared place.
- **Project URLs and DNS**: URL validation does not resolve host names, so a name that resolves to a loopback or metadata address (or changes after validation) passes. Fix on the gateway side with an egress allow-list or a resolving handler.
- **Last admin**: nothing stops an admin from removing their own `Admin` membership on core or that of the last admin. Add a check (at least one admin left).
- **Retroactive default roles**: setting `DefaultRoleId` later does not grant it to existing users. An explicit "grant to everyone" admin action could do it if needed.
- **Profiles of deleted or deactivated accounts**: identity cannot delete or deactivate accounts yet; when it can, it should tell core (an event to remove or mark the profile).
- **Audit log** of access changes (who granted or revoked what, when); `GrantedByUserId` only records the latest grant.
- **Project requests and expiring grants** (users asking for access, `ExpiresAt` on a membership).
- **Monitoring**: a status view of project health (from the gateway's probes), Uptime Kuma or OpenTelemetry.

## Future / maintenance
- **Queryable logs in a database**, for every action: (1) application logs (errors, warnings), for example a Serilog sink; (2) an audit/action log (who did what, when, status, duration), a good fit for MongoDB. The correlation id from platform-kit is already in every log scope.
- Tests that need a real database (phase 2, not built): the EF mappings, the cascades (a revoked membership removes the saved tab, a deleted project removes its roles and members), unique keys, `citext` and the migrations. `CoreApi.Tests` covers the services and rules with in-memory repositories; a Postgres container (Testcontainers) in CI would cover the rest.
- More profile fields (display name, avatar, bio, ...).

## TODO: publish the OpenAPI schema in every environment

- `Program.cs` maps OpenAPI only in Development. Map it at `/api/openapi/v1.json` with `AllowAnonymous()` in all environments (the gateway then serves it at `/api/core/openapi/v1.json`). Keep Swagger UI Development-only.
- Path convention so the schema does not depend on the environment prefix (`/dev`, `/preprod`, `/prod`): strip the leading `/api` from the documented paths and set the server URL to the relative `..`, which resolves against the schema's own URL (`/<env>/api/core/openapi/v1.json` becomes `/<env>/api/core/`). Do it in a document transformer next to `BearerSecuritySchemeTransformer`; keep the Bearer security scheme.
- The same applies to identity-api. Context and design: platform-kit/docs/backlog.md.
- Parked idea: Swagger UI pages on dev only, served by Caddy straight from the containers (`/dev/docs/<api>/swagger/index.html`), with only the docs paths allowed through.

## Review findings not fixed yet (2026-11-09)
- **Project URLs are not resolved**: `ProjectUrlRules` checks the text only and allows private ranges (containers need them). An admin could point a project's `FrontendUrl` at an internal service, and that service's unauthenticated pages (for example `/health`) would become public at `/apps/<slug>/`. Internal endpoints still need the internal key, which the gateway strips. Admin-only; a list of host names that must not be used as project URLs (the platform's own services) would close it.
