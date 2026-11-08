# CLAUDE.md

Instructions for `mock-web/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

A throw-away project frontend (plain TypeScript, Vite, no framework; slug `mock`) that runs in an iframe of the harness. It has no data and no business logic. Its only job is to test the harness end to end: the tab, the iframe served by the gateway at `/apps/mock/`, the bridge from [platform-kit-web](../platform-kit-web/CLAUDE.md) (init, API calls through the harness, deep links, theme, role and logout events) and the `mock-api` behind it. It is deployed with the `mock` compose profile, next to `mock-api`, and registered by setting the `mock` project's `frontendUrl` (as a core admin).

## Commands

Run from `mock-web/`:

- Install: `npm install`
- Dev server: `npm run dev` (it only does something useful inside the harness, so prefer the deployed or local-deploy stack)
- Check (Biome + type check): `npm run ci`; fix formatting: `npm run check`
- Build: `npm run build`
- Docker image (repo root as context, because of `platform-kit-web`): `docker build -f mock-web/Dockerfile -t mock-web .`

## Rules

- Keep it tiny. New screens only if they help test the harness or the bridge.
- It must stay a normal consumer of `platform-kit-web/sdk`: no copy of the bridge code, and no token handling (the harness makes the API calls).
- Build with `base: "./"` and hash routing: the harness serves apps under a path that differs per environment (`/dev/apps/mock/`, `/apps/mock/`).
- No inline scripts or styles and no third-party origins: the harness serves apps with a strict Content-Security-Policy (`harness-web/nginx/apps-headers.conf`).
- Ask before adding dependencies.

## Documentation

Keep `docs/` up to date at the end of every step: `implementation.md` and `changelog.md`.
