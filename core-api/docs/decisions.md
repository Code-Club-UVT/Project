# Decisions

Account, token and email decisions moved to identity-api/docs/decisions.md and email-api/docs/decisions.md with the split.

| Decision | Reason |
|---|---|
| Controllers, not minimal APIs | Project requirement |
| PostgreSQL + EF Core code-first | Project requirement |
| Multiple data stores: PostgreSQL (relational), MongoDB (documents), Neo4j (graph) | Each store fits a different kind of data |
| Data code is split per store (`Data/Postgres/`, later `Data/Mongo/`, `Data/Neo4j/`); the context is named `PostgresDbContext`; connection strings are `PostgresConnection`, `MongoConnection`, `Neo4jConnection` | `DbContext` is relational-only, so a generic `AppDbContext` was misleading; naming by store makes it clear which database code touches |
| N-layer architecture (Controller, Service, Repository) in a single project with folders | Clear responsibilities; the interface boundaries allow splitting into projects later |
| Generic `IRepository<TEntity, TKey>` with specific repositories on top; interfaces for every repository | Multiple data stores and mockable tests; queries beyond the basics live in specific repositories |
| `GetAll` is always paged (size capped at 100) and ordered by primary key | An unbounded `GetAll` is dangerous on real tables; stable ordering is needed for paging |
| Repositories never call `SaveChanges`; the service commits through `IUnitOfWork` | Related writes (a project and its roles, a membership and its grant) must commit together |
| No `IQueryable` outside the repositories | Keeps EF out of the service layer, needed for multiple stores |
| Services return `Result<T>` for expected failures, exceptions only for unexpected ones | Explicit control flow, no exceptions for normal outcomes |
| One `GlobalExceptionHandler` (500 ProblemDetails) instead of try/catch in every controller | Same behavior written once; controllers stay thin |
| HTTP status follows the error type (400/401/403/404/409), errors are ProblemDetails with a `code` field | The SPA can tell user mistakes from server failures |
| Success returns 200 (with a value) or 204 (without) | Keeps the "200 on OK" convention |
| Hand-written mapping, DataAnnotations validation, no extra packages | No dependency (AutoMapper is commercial); enough at this size |
| DTOs live in `Models/` (`Requests/`, `Responses/`), not `Dtos/` | Project naming preference |
| Endpoints require authentication by default (fallback policy); anonymous ones opt out with `[AllowAnonymous]` | A forgotten attribute cannot expose an endpoint |
| Rate limiting and CORS moved from core-api to the gateway (replaced the built-in limiter at 10 requests per minute per IP on the auth endpoints) | The gateway is the single entry point, so limits and CORS live in one place; core-api must then only be reachable through it |
| Swagger UI gets a Bearer scheme through an OpenAPI document transformer | Lets the Authorize button work for manual testing |
| `Services/` is grouped by feature (Users, Projects, ProjectRoles, ProjectMembers, Seeding), interface next to implementation; errors live next to the feature that owns them | One feature in one place; repositories use `Abstractions/` because they have several implementations per store, services have one |
| `docker-compose.yml` lives in `core-api/`, with `.env.example` and `docker/` for supporting files | Infrastructure for this API, kept inside the project (the repo root also holds the thesis) |
| PostgreSQL 17 pinned by major version, data in a named volume, port published on `127.0.0.1` only | Data survives container restarts; the database is not reachable from the network |
| pgAdmin 9 runs only with the `tools` compose profile, in desktop mode (no login), with the database pre-registered | Handy locally, but a database admin UI must not run by default on a server |
| Guid primary keys | Safe to expose in tokens and URLs |
| Swagger UI via `Swashbuckle.AspNetCore.SwaggerUI` over the built-in OpenAPI document | The .NET 10 template has no UI; this adds only the UI and keeps the built-in generator |
| Every API lives behind the gateway at `/api/<slug>`; core-api is the system project `core` | One public origin (no CORS between APIs, one TLS endpoint, one place for limits, logs and auth) |
| Authorization is by role only; no permission columns | Out of scope for now; the project role name is the `role` claim |
| One role per user per project (PK `(UserId, ProjectId)`); a composite FK ties the role to the same project | Simple model; the database prevents a role from another project |
| Public path is derived from the slug; `BackendUrl` may include a path (core: `.../api`, identity: `.../api`) | No path column or strip flag; identity's `/api/identity/auth` becomes `/api/auth` upstream |
| One `ProjectsController` holds the project, role and member endpoints | Nested routes (`/api/projects/{id}/roles`) without relying on how route attributes combine across base classes |
| Project URLs are validated (http/https, no credentials, no loopback/link-local/metadata outside Development); private ranges allowed | The gateway calls these URLs, so an admin account must not be able to aim it at the host or a metadata service. Containers need private ranges |
| `/health` checks PostgreSQL; `HealthUrl` per project for the gateway's probes | Cheap to add now; a monitoring dashboard is deferred (backlog) |
| `UseHttpsRedirection` removed from core-api | Its only caller is the gateway over plain http; with an https port configured (for example the `https` launch profile) the redirect made JWKS fetches, token exchange and proxied calls answer 307 (found by code review). TLS belongs in front of the gateway |
| Accounts, tokens and email moved out (identity-api, email-api); core keeps profiles, projects, roles, members | Smaller services with one job each; see identity-api/docs and email-api/docs for their decisions |
| Core is an `Exchange` project (`aud=core`), validated by platform-kit like every API | One validation path for all APIs; core no longer needs the signing key or edge-token validation |
| Platform admin is the `Admin` role of the `core` project; the global `UserRole` is gone | Rights live in one model; the Admin role of core cannot be renamed or deleted (it would lock admins out) |
| `UserProfiles` keeps a read copy of username and email, updated by identity's events | Admin and member lists need them without calling identity; identity stays the owner (no unique index here) |
| Default roles: optional `Projects.DefaultRoleId`, granted when identity reports the email confirmed; not retroactive | Opt-in per project (others stay invite-only); unconfirmed accounts cannot log in anyway, so they never hold unused access |
| `DefaultRoleId` is a plain FK with `ON DELETE SET NULL`; the service checks the role belongs to the project | A composite FK through the project's own key confuses EF; deleting the default role simply makes the project invite-only |
| The first admin is whoever confirms with `Seed:AdminEmail`: they get `Admin` on core instead of its default role | Identity creates the account, core decides the rights; no admin flag crosses services |
| `/internal/users` (upsert) and `/internal/users/{id}/confirmed` are idempotent; confirmed answers 404 until the profile exists | Identity's outbox can deliver twice or out of step; a 404 makes it retry later |
| CHECK constraint: only a system project can be `PassThrough` | A row written another way must not make the gateway forward edge tokens to an admin-chosen URL (open point from the code review) |
| Own schema `core` in the shared `platform` database; fresh initial migration | The split started from an empty database (no data worth keeping) |
| `ResultControllerBase`, `Result` and the exception handler come from platform-kit | Same error format in every service |
| `BaseUrl` renamed `BackendUrl` and made nullable; `FrontendUrl` added (nullable) | A project can have a backend, a frontend, both or neither. The gateway builds `/api/<slug>` from the backend and `/apps/<slug>` from the frontend. The migration renames the column instead of dropping it, so existing URLs survive (EF scaffolds drop + add; edited by hand) |
| `me/projects` returns `hasFrontend`/`hasBackend`, not the URLs | The URLs are internal addresses (Docker host names); the browser only ever uses `/apps/<slug>/` and `/api/<slug>` |
| Saved tabs (`UserTabs`) are keyed by project id, per user, and the whole list is replaced by one `PUT`; the FK to the membership cascades | The harness already has the ids from `me/projects`. The database removes tabs of lost access or deleted projects, so no cleanup code is needed. Last write wins across browsers (accepted); updating rows in place avoids deleting and re-adding the same key in one save |
