# CLAUDE.md

Instructions for `core-api/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

The main API. ASP.NET Core Web API on .NET 10, using controllers (not minimal APIs). Project: `CoreApi`.

## Commands

Run from `core-api/`:

- Build: `dotnet build`
- Run: `dotnet run` (default URL in `Properties/launchSettings.json`)
- Add a migration: `dotnet ef migrations add <Name> --output-dir Data/Postgres/Migrations`
- Apply migrations: `dotnet ef database update`
- Start the dev database: `docker compose up -d` (add `--profile tools` for pgAdmin at http://localhost:5050); stop with `docker compose down`. `docker compose down -v` deletes the data, so only run it when asked.

## Rules

- Use controllers in `Controllers/`; do not add minimal API endpoints.
- Ask before adding NuGet dependencies.
- Several data stores are used: PostgreSQL (relational, EF Core code-first), MongoDB (documents) and Neo4j (graph). Keep each store's code in its own folder under `Data/` and name its context/client after the store (`PostgresDbContext`). Connection strings: `PostgresConnection`, `MongoConnection`, `Neo4jConnection`.
- PostgreSQL: relational entities in `Entities/`, one `IEntityTypeConfiguration` per entity in `Data/Postgres/Configurations/`, migrations in `Data/Postgres/Migrations/`. Never edit an applied migration; add a new one.
- Never store raw passwords or tokens; only hashes. Never log them either (the Development-only `LoggingEmailSender` is the one exception).
- Endpoints require authentication by default; anonymous ones must say `[AllowAnonymous]`. Check roles with `[Authorize(Roles = ...)]`.
- Configuration lives in `appsettings*.json` and can be overridden by environment variables (`Section__Key`). Committed secrets must be clearly marked placeholders (`CHANGE-ME...`); real values come from the environment.

## Architecture

N-layer (Controller, Service, Repository) in this single project; details in [docs/architecture.md](docs/architecture.md). Rules:

- Controllers are thin: call a service, return `ToActionResult(result)` from `ApiControllerBase`. No business logic, no try/catch (`GlobalExceptionHandler` handles unexpected errors), no entities.
- Services live in a folder per feature under `Services/` (`Auth/`, `Users/`, `Tokens/`, `Email/`, `Seeding/`), with the interface next to its implementation and the feature's errors (`XxxErrors`) in the same folder. Put a new service in its feature's folder, or create a new feature folder with namespace `CoreApi.Services.<Feature>`.
- Services hold all business logic, validation of business rules and error handling, and map between models and entities. They return `Result` / `Result<T>` for expected failures and throw only for unexpected ones. Entities never leave the service layer.
- Repositories use only entities, are behind interfaces, and never call `SaveChanges`; services commit through `IUnitOfWork`. Do not expose `IQueryable`. `GetAll` is always paged.
- DTOs go in `Models/Requests` and `Models/Responses` (not `Dtos`); hand-written mapping in `Mappings/`.
- All I/O is async with a `CancellationToken`; keep pure code synchronous.

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)):

- `implementation.md`: what exists now and how to work with it
- `auth-design.md`: authentication design and security rules
- `decisions.md`: decisions taken, with the reason
- `backlog.md`: open questions, future work, things to revisit (for example the token cleanup job)
- `changelog.md`: what was done in each step

Read `docs/` at the start of a session to see the current state.
