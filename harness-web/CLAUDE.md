# CLAUDE.md

Instructions for `harness-web/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

The harness: a React single-page app that stands for the platform's front end. For now it is a test page (register, log in, log out, and calls through the gateway that show the project token at work). It talks only to the gateway, through `/api` on its own origin. In every environment the container also holds the nginx that forwards `/api` to the gateway on the private network.

Read [docs/implementation.md](docs/implementation.md) first, and [../identity-api/docs/auth-design.md](../identity-api/docs/auth-design.md) for the token design the app follows.

## Commands

Run from `harness-web/`:

- Install: `npm ci`
- Dev server: `npm run dev` (http://localhost:5173; `/api` is proxied to `VITE_PROXY_TARGET`, see `.env.example`)
- Check (Biome lint + format check, then type check): `npm run ci`; fix formatting and safe lint issues: `npm run check`
- Build: `npm run build`
- Docker image (repo root as context): `docker build -f harness-web/Dockerfile -t harness .`

## Rules

- Stack: React, TypeScript, Tailwind v4, `@tanstack/react-query`, `axios`, `i18next`. Biome is the only linter and formatter (no ESLint, no Prettier). Ask before adding dependencies.
- All API calls go through the `http` instance in `src/api/http.ts` (token, refresh on 401, retries). Never call `fetch` or a bare `axios` for the API. Only the refresh call uses the bare instance.
- The access token stays in memory (`src/auth/tokenStore.ts`). Never put tokens in localStorage, sessionStorage, cookies written by JS, or URLs.
- No inline scripts or styles and no third-party origins: the Content-Security-Policy in `nginx/security-headers.conf` forbids them. Keep `theme-init.js` and `config.js` as external files.
- No environment-specific values in the build. Where the app lives and where the API is are read at runtime from `config.js`, which the container renders from environment variables (`nginx/entrypoint.sh`).
- User-visible text goes in both `src/i18n/locales/en.json` and `ro.json`.
- Keep `nginx/` and the Dockerfile in step with the server's compose file (`stack-v2/docker-compose.yml` in `~/codeclub-uvt`, see the root `CLAUDE.md` for how the server is handled).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
