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
Controllers/                   thin; ApiControllerBase converts Result to HTTP
Services/                      business logic, grouped by feature (interface next to its implementation)
  Auth/                        IAuthService/AuthService (login, refresh, logout), IAccountService/AccountService
                               (register, confirm email, reset/change password), AuthErrors, AuthSession
  Users/                       IUserService/UserService, UserErrors
  Tokens/                      ITokenService/TokenService (JWT, random tokens, hashing), AccessToken
  Email/                       IEmailSender, LoggingEmailSender (Development only)
  Seeding/                     AdminSeeder
Models/Requests, Responses/    DTOs exchanged with the API clients
Mappings/                      entity <-> model mapping (hand-written)
Options/                       strongly typed configuration classes (Jwt, RefreshToken, Auth, Frontend, Seed)
Repositories/Abstractions/     IRepository<,>, IUnitOfWork, specific repository interfaces
Data/Postgres/                 PostgresDbContext, Configurations, Migrations
Data/Postgres/Repositories/    EF Core implementations of the repository interfaces
Entities/                      relational entities (PostgreSQL)
Common/                        Result types, paging, GlobalExceptionHandler
Extensions/                    DI registration (AddPostgres, AddRepositories, AddServices, AddAuth, AddRateLimiting, AddFrontendCors), ClaimsPrincipal helpers
```

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
- `ApiControllerBase` maps the error type to a ProblemDetails (RFC 9457) response, with the error `Code` as an extension field:

| ErrorType | HTTP |
|---|---|
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |

- Success: `Result<T>` gives 200 with the value; `Result` without a value gives 204.
- Unexpected exceptions: `GlobalExceptionHandler` logs them and returns a 500 ProblemDetails. The exception message is included only in Development.

## Pipeline order (`Program.cs`)

Exception handler, (Development: OpenAPI + Swagger UI), HTTPS redirection, CORS, rate limiter, authentication, authorization, controllers. Authentication and authorization are described in [auth-design.md](auth-design.md). Endpoints require a logged-in user by default (fallback policy); anonymous endpoints must say `[AllowAnonymous]`.

## Async

All I/O is async and takes a `CancellationToken`. Pure code (mapping, validation rules) is synchronous.
