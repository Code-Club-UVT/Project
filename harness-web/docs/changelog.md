# Changelog

## Step 1: the harness
- Created `harness-web`: Vite, React 19, TypeScript, Tailwind v4, react-query, axios, i18next (English and Romanian), light/dark/system theme, Biome
- Test page: register, log in, log out, and `whoami` calls through the gateway (core profile, core projects, mock project)
- axios instance with the access token, one shared refresh on 401 and retries for transient failures
- Dockerfile (Biome and type check, build, nginx-unprivileged) and nginx config: serves the app, forwards `/api` to the gateway, `open` and `restricted` modes, CSP and other headers, client IP handling; settings rendered at start from environment variables
- Workflow builds the `harness` image
- Server (not in git): `harness` service in `stack-v2/docker-compose.yml`; Caddy routes each environment to its harness, with security headers, and no longer has a route to any gateway; read-only, capability-free containers for all app services; `init-v2.sh` and the identity template know the prod root path and the harness variables
- Verified on dev after a manual workflow run on this branch: login, silent refresh after a reload, `/core/users/me` and `/core/users/me/projects` through the gateway (project token minted by identity), logout, translated error messages, security headers, restricted mode (`/health` and OpenAPI paths 404, other methods refused), real client IP in nginx, rate limit. Not exercised live: the 401-refresh-replay path (needs an expired access token) and the mock project call (no `mock` project exists on dev)
- Found while deploying: Biome formatting the markdown docs failed the image build (markdown is now excluded); `no-new-privileges` made every container fail to start (removed); `/healthz` was reachable from outside (`allow`/`deny` do not apply to `return`; fixed with a `$remote_addr` check)
- `local-deploy/` added at the repo root: the whole stack on your own machine (see its README)
- Not done: preprod was not moved to stack-v2 (it would stay down until the next merge to main; see backlog)

## Harness shell: tabs and iframes
- Hash router (`react-router-dom`), tabs from `me/projects` (projects with a frontend), one lazy, kept-alive iframe per app at `<base path>apps/<slug>/`
- Bridge host from the new `platform-kit-web` (file dependency): init, API calls through the harness, deep-link path sync, events (theme, locale, logout, role-changed)
- nginx `/apps/` location and headers; Vite dev proxy for `/apps`; the Dockerfile copies `platform-kit-web` before `npm ci`
- vitest added (17 tests); `npm run ci` now runs them
