# CLAUDE.md

Instructions for `mock-api/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

A throw-away project API (ASP.NET Core, .NET 10, project `MockApi`, slug `mock`) built on [platform-kit](../platform-kit/CLAUDE.md). It has no data and no business logic. Its only job is to test the whole chain in a deployed environment: browser, reverse proxy, gateway, project token, a real API. It is deployed to preprod and prod (compose profile `mock`) and is registered as a project in core-api by hand (as a core admin).

## Commands

Run from `mock-api/`:

- Build: `dotnet build`
- Run: `dotnet run` (http://localhost:5200, Development; the JWKS URL points at identity-api on http://localhost:5060)
- Docker image (repo root as context, because of `platform-kit`): `docker build -f mock-api/Dockerfile -t mock-api .`

## Rules

- Keep it tiny. New endpoints only if they help test the platform (for example a new platform-kit feature).
- It must stay a normal consumer of `platform-kit`: no copy of token validation code.
- Ask before adding NuGet dependencies.
- Settings come from `appsettings*.json` and environment variables (`PlatformAuth__JwksUrl`, ...).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)).
