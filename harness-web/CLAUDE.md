# CLAUDE.md

Instructions for `harness-web/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

The harness: a React single-page app that stands for the platform's front end. After login it shows one tab per project the user belongs to that has a frontend; each project's frontend runs in an iframe (`/apps/<slug>/`, served by the gateway) and makes its API calls through the harness (bridge in `../platform-kit-web`). The home tab is still the test page (register, log in, log out, and calls through the gateway that show the project token at work). It talks only to the gateway, through `/api` and `/apps` on its own origin. In every environment the container also holds the nginx that forwards `/api` to the gateway on the private network.

Read [docs/implementation.md](docs/implementation.md) first, and [../identity-api/docs/auth-design.md](../identity-api/docs/auth-design.md) for the token design the app follows.

## Commands

Run from `harness-web/`:

- Install: `npm ci`
- Dev server: `npm run dev` (http://localhost:5173; `/api` is proxied to `VITE_PROXY_TARGET`, by default the stack of `../local-deploy` on http://localhost:8080, see `.env.example`)
- Check (Biome lint + format check, type check, unit tests): `npm run ci`; tests alone: `npm test`; fix formatting and safe lint issues: `npm run check`
- Build: `npm run build`
- Docker image (repo root as context, because of `platform-kit-web`): `docker build -f harness-web/Dockerfile -t harness .`

## Rules

- Stack: React, TypeScript, Tailwind v4, `@tanstack/react-query`, `react-router-dom` (hash routing), `axios`, `i18next`, tests with `vitest`, and `platform-kit-web` (file dependency). Biome is the only linter and formatter (no ESLint, no Prettier). Ask before adding dependencies.
- Apps in iframes never get the token: they ask the harness (`src/apps/fetchApi.ts` uses the same `http` instance). Apps are first-party and same-origin; do not build features that assume untrusted apps. Tabs come from `GET /core/users/me/projects` and are only a convenience; access is enforced by the gateway.
- All API calls go through the `http` instance in `src/api/http.ts` (token, refresh on 401, retries). Never call `fetch` or a bare `axios` for the API. Only the refresh call uses the bare instance.
- The access token stays in memory (`src/auth/tokenStore.ts`). Never put tokens in localStorage, sessionStorage, cookies written by JS, or URLs.
- No inline scripts or styles and no third-party origins: the Content-Security-Policy in `nginx/security-headers.conf` forbids them. Keep `theme-init.js` and `config.js` as external files.
- No environment-specific values in the build. Where the app lives and where the API is are read at runtime from `config.js`, which the container renders from environment variables (`nginx/entrypoint.sh`).
- User-visible text goes in both `src/i18n/locales/en.json` and `ro.json`.
- Keep `nginx/` and the Dockerfile in step with the server's compose file (`stack-v2/docker-compose.yml` in `~/codeclub-uvt`, see the root `CLAUDE.md` for how the server is handled).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
