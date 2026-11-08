# CLAUDE.md

Instructions for `platform-kit-web/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

The shared code of the platform's front ends, the web counterpart of the backend `platform-kit`. Today it holds the **bridge** (`src/sdk`) between the harness and the apps that run in iframes of the harness: the harness side (`createAppHost`) and the app side (`createAppClient`). It has no runtime dependencies and no React, so any app can use it.

It is consumed by file path (`"platform-kit-web": "file:../platform-kit-web"`), not through a registry, and its TypeScript sources are imported directly (the consumer's bundler compiles them). Consumers: `harness-web` (host) and every app (client), for example `mock-web`.

A second entry point, `platform-kit-web/ui` (shared React components, theme and i18n helpers), is planned; add it when the first shared component moves here. The `sdk` entry point must never import from it.

## Commands

Run from `platform-kit-web/`:

- Install: `npm install`
- Tests: `npm test` (vitest, node environment: the bridge is tested with fake windows)
- Check: `npm run check` (Biome, fixes formatting) and `npm run typecheck`
- Everything CI runs: `npm run ci`

## Rules

- Apps are first-party and same-origin with the harness. The bridge is not a security boundary against untrusted apps: the harness checks the origin and that the message comes from a registered iframe, the token never reaches the app (the harness makes the API calls), but a same-origin app could read the parent anyway. Do not use it for third-party apps.
- Keep the protocol small and versioned (`PROTOCOL` in `protocol.ts`). Change it only together with the harness and the apps.
- Ask before adding dependencies.
- Biome formatting is enforced in the images that consume this package; run `npm run check` before finishing.

## Documentation

Keep `docs/` up to date at the end of every step: `implementation.md` (what exists and how to use it) and `changelog.md`.
