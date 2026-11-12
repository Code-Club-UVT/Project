# Architecture

N-layer architecture in a single project, separated by folders. Dependencies point downward only, and each layer depends on the interface of the layer below.

```
Controller  ->  Service  ->  Repository  ->  data store (PostgresDbContext, later Mongo, Neo4j)
 Models          Models +      Entities
 (requests/      Entities
  responses)
```

## Folder map

```
Controllers/                   thin; ApiControllerBase (api/[controller]) on platform-kit's ResultControllerBase, which converts Result to HTTP
Services/                      business logic, grouped by feature (interface next to its implementation)
  Users/                       IUserService/UserService (profile, admin list), IUserProvisioningService/UserProvisioningService
                               (identity's events: profile upsert, default roles on confirmation), UserErrors
  Projects/                    IProjectService/ProjectService, ProjectUrlRules, CoreRoles, ProjectErrors
  ProjectRoles/, ProjectMembers/
  UserTabs/                    IUserTabService/UserTabService (the user's saved open tabs), UserTabRules, UserTabErrors
  Seeding/                     ProjectSeeder (core and identity projects, core roles)
Models/Requests, Responses/    DTOs exchanged with the API clients
Mappings/                      entity <-> model mapping (hand-written)
Options/                       strongly typed configuration (Seed)
Repositories/Abstractions/     IRepository<,>, IUnitOfWork, specific repository interfaces
Data/Postgres/                 PostgresDbContext (schema core), Configurations, Migrations, DesignTimeDbContextFactory
Data/Postgres/Repositories/    EF Core implementations of the repository interfaces
Entities/                      relational entities (PostgreSQL)
Common/                        PostgreSQL health check, Swagger Bearer transformer
Extensions/                    DI registration (AddPostgres, AddRepositories, AddServices, AddAuth), ClaimsPrincipal helper
```

`Result`, `Error`, `PagedResult`, `ResultControllerBase` and the exception handler come from platform-kit (shared by identity, core and email).

## Layer rules

### Controllers
- Only call a service and return `ToActionResult(result)`. No business logic, no try/catch, no entities.
- Derive from `ApiControllerBase` (`[ApiController]`, route `api/[controller]`).
- Input shape validation is done on the request models (DataAnnotations); `[ApiController]` returns 400 automatically.

### Services
- All business logic, business validation, and error handling.
- Map between models and entities in both directions. Entities never leave the service layer, and models never reach the repositories.
- Return `Result` / `Result<T>` for expected failures (validation, not found, conflict, bad credentials). Throw only for unexpected failures; do not catch exceptions just to hide them.
- Call `IUnitOfWork.SaveChangesAsync` once at the end of an operation, so related writes commit together.
- Log important events with `ILogger` (never passwords or tokens).

### Repositories
- Work only with entities. No DTOs, no business rules.
- `IRepository<TEntity, TKey>`: `GetAllAsync(page, pageSize)` (paged, page size capped at 100, ordered by primary key), `GetByIdAsync` (null when missing), `CreateAsync`, `UpdateAsync`, `DeleteAsync`.
- Relational (Postgres) repositories never call `SaveChanges`; the service commits through `IUnitOfWork`.
- Do not expose `IQueryable`. Queries beyond the basics go in specific repositories (`IUserRepository : IRepository<User, Guid>`, with methods such as `GetByEmailAsync`), added when a feature needs them.
- One implementation per data store. `IUnitOfWork` is the relational one; MongoDB and Neo4j will have their own transaction handling.

## Error handling

- Expected failures: `Result` with an `Error` (`Code`, `Message`, `Type`).
- `ResultControllerBase` (platform-kit) maps the error type to a ProblemDetails (RFC 9457) response, with the error `Code` as an extension field:

| ErrorType | HTTP |
|---|---|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |

- Success: `Result<T>` gives 200 with the value; `Result` without a value gives 204.
- Unexpected exceptions: platform-kit's exception handler (`AddPlatformProblemDetails`) logs them and returns a 500 ProblemDetails. The exception message is included only in Development.

## Pipeline order (`Program.cs`)

Exception handler, (Development: OpenAPI + Swagger UI), authentication, request context, authorization, controllers, `/health`. Authentication: project tokens with audience `core` (platform-kit, default scheme) and identity's internal key on `/internal/...`; the token design is in identity-api/docs/auth-design.md. TLS, CORS and rate limiting are the gateway's job. Endpoints require a logged-in user by default (fallback policy); anonymous endpoints must say `[AllowAnonymous]`.

## Async

All I/O is async and takes a `CancellationToken`. Pure code (mapping, validation rules) is synchronous.
