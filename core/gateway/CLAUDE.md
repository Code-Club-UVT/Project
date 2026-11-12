# CLAUDE.md

Instructions for `core/gateway/`. The root [CLAUDE.md](../../CLAUDE.md) also applies.

## Overview

The single entry point for every browser request: a YARP reverse proxy (ASP.NET Core, .NET 10, project `Gateway`). It validates the edge token, checks that the user may use the project, swaps the token for a project token (identity issues it) and forwards the request. Routes come from the `core.Projects` table that core-api owns. The gateway never writes to the database.

Read [../identity-api/docs/auth-design.md](../identity-api/docs/auth-design.md) for the token design and [../../common/platform-kit/CLAUDE.md](../../common/platform-kit/CLAUDE.md) for the shared library the gateway uses to validate the edge token.

## Commands

Run from `core/gateway/` (identity-api, core-api and PostgreSQL must be running):

- Build: `dotnet build`
- Test: `dotnet test Gateway.Tests` (no Docker or database needed)
- Run: `dotnet run` (http://localhost:5100, Development). Identity must be at http://localhost:5060 and core at http://localhost:5054 (their default launch URLs), because the seeded project rows point there.
- Docker image (repo root as context, because of `platform-kit`): `docker build -f core/gateway/Dockerfile -t gateway .`

## Rules

- No business logic and no data of its own: it reads `core.Projects`, `core.ProjectMembers` and `identity.Users` (read-only models in `Data/`) and asks identity for tokens. User, role and project management belongs to core-api; accounts and tokens to identity.
- Never write to the database. In production use a database user with SELECT only on those three tables.
- Fail closed: if access cannot be checked (database or identity down and no usable cached value), answer 503, never forward the request.
- The gateway is the only backend that may be reached from outside, and only through the harness container's nginx (`core/harness-web/nginx`), which forwards `/api` to it; Caddy has no route to it. Identity, core-api, email-api and all project APIs must be on a private network. The `/apps/<slug>/` routes are anonymous and not membership-checked (an iframe navigation cannot send `Authorization`): they serve public files only, so never put data or secrets in a frontend; the API stays protected. They forward no `Authorization`, cookie or identity headers. It removes the client's `X-User-*`, `X-Internal-Key` and `Forwarded` headers, replaces `Authorization` with the project token and sets `X-Forwarded-*` itself; keep doing that when adding code.
- `/internal/...` and `/.well-known/...` of any service must never get a route (the gateway only routes `/api/<slug>/...`, which every project's `BackendUrl` maps below `/api`, and `/apps/<slug>/...`, the static frontend of a project). Email-api is not a project and has no route at all.
- YARP reserves the policy names `default` and `disable` for CORS and rate limiting; use other names.
- Ask before adding NuGet dependencies.
- Configuration lives in `appsettings*.json` and can be overridden by environment variables (`Section__Key`). Committed secrets must be clearly marked placeholders (`CHANGE-ME...`).

## Tests

`Gateway.Tests/` is its own project (xunit, fakes written by hand, no mocking library) inside this folder; the API project excludes it and the Docker context leaves it out. A change to behaviour comes with a test. `.github/workflows/ci.yml` runs it on every pull request and before every deployment, so a failing test blocks a deploy. Tests that need a real database are not built yet (see the backlog).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
