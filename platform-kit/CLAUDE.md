# CLAUDE.md

Instructions for `gateway-auth/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

A small shared class library (`GatewayAuth`, .NET 10) that every API behind the gateway references. It validates the project tokens the core API issues and gives the API a request context (user, role, correlation id). It is not a web app and has no business logic.

## Commands

Run from `gateway-auth/`:

- Build: `dotnet build`

APIs reference it by project reference (`<ProjectReference Include="..\gateway-auth\GatewayAuth.csproj" />`). Because of that, an API's Dockerfile must use the repo root as build context.

## Rules

- Keep it small and generic: authentication, request context, health. No code that belongs to one API.
- Validation must stay strict and fail closed: pinned algorithm (ES256), issuer, audience (this API's slug), lifetime, signature from the published keys. Do not add switches that weaken it. Loosening it affects every API.
- The public surface is the extension methods in `GatewayAuthExtensions` and `IRequestContext`; everything else is `internal`. Changing either is a change for all APIs, so note it in `docs/changelog.md`.
- Ask before adding NuGet dependencies.
- Settings come from the `GatewayAuth` configuration section and can be overridden by environment variables (`GatewayAuth__Audience`, ...).
- All claim names (`sub`, `username`, `role`) must match what core-api's `TokenService` writes; change both together.

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
