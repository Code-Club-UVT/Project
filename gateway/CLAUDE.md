# CLAUDE.md

Instructions for `gateway/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

The single entry point for every browser request: a YARP reverse proxy (ASP.NET Core, .NET 10, project `Gateway`). It validates the edge token, checks that the user may use the project, swaps the token for a project token (core API issues it) and forwards the request. Routes come from the `Projects` table that core-api owns. The gateway never writes to the database.

Read [../core-api/docs/auth-design.md](../core-api/docs/auth-design.md) for the token design and [../gateway-auth/CLAUDE.md](../gateway-auth/CLAUDE.md) for the shared library the gateway uses to validate the edge token.

## Commands

Run from `gateway/` (core-api and PostgreSQL must be running; see `core-api/CLAUDE.md`):

- Build: `dotnet build`
- Run: `dotnet run` (http://localhost:5100, Development). The core API must be at http://localhost:5054 (its default launch URL), because the `core` project row points there.
- Docker image (repo root as context, because of `gateway-auth`): `docker build -f gateway/Dockerfile -t gateway .`

## Rules

- No business logic and no data of its own: it reads `Projects`, `ProjectMembers` and `Users` (read-only models in `Data/`) and asks core-api for tokens. User, role and project management belongs to core-api.
- Never write to the database. In production use a database user with SELECT only on those three tables.
- Fail closed: if access cannot be checked (database or core-api down and no usable cached value), answer 503, never forward the request.
- The gateway is the only thing that may be reachable from outside. Core-api and all project APIs must be on a private network. It removes the client's `X-User-*`, `X-Internal-Key` and `Forwarded` headers, replaces `Authorization` with the project token and sets `X-Forwarded-*` itself; keep doing that when adding code.
- `/internal/...` of core-api must never get a route.
- YARP reserves the policy names `default` and `disable` for CORS and rate limiting; use other names.
- Ask before adding NuGet dependencies.
- Configuration lives in `appsettings*.json` and can be overridden by environment variables (`Section__Key`). Committed secrets must be clearly marked placeholders (`CHANGE-ME...`).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
