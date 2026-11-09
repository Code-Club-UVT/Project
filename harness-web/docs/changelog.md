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

## Throttle message
- `Auth.TooManyAttempts` (login blocked after too many failures) has a translated message in English and Romanian

## Saved tabs, app menu and clean URLs
- The home page has an app menu (icon, name, role) of the apps the user can open; tabs exist only for open apps, with a close button
- The open tabs are saved per user in core (`GET/PUT /core/users/me/tabs`, the whole list, debounced) and restored after login, filtered by the apps the user can still open; a deep link opens its tab
- `AppIcon` shows the app's `icon.svg` (served by the app at `/apps/<slug>/icon.svg`) or a default icon
- The harness no longer uses hash routing: `BrowserRouter` with the base path as basename, URLs like `/dev/app/mock/about`; the apps in the iframes keep hash routes
- New files: `api/tabs.ts`, `apps/{OpenTabs.tsx,tabList.ts,debouncedSaver.ts,AppIcon.tsx,AppMenu.tsx}` and tests; 33 tests in total
- Verified on dev (commit 0281416, manual workflow run, signed in as the dev admin): the menu shows `Mock` with its icon and role and no tab; a click opens `/dev/app/mock` with the iframe ("Hello admin"); About in the app changes the address to `/dev/app/mock/about` (no `#`); the path is saved after about a second; a fresh page load restores the tab at `/about` without loading its frame until clicked; closing the tab removes the frame, goes home and saves an empty list; a direct visit to `/dev/app/mock/about` opens the tab; `PUT` rejects `//evil.com`, a backslash, `http://x` and 21 tabs with 400 (`UserTab.InvalidPath`, `UserTab.TooMany`), drops a project the user is not a member of and keeps the first of a repeated project; revoking the membership removed the saved tab (row gone, still gone after re-granting); a broken icon shows the default icon; an unknown slug redirects home; `/dev` redirects to `/dev/`. Not tested: the login form itself (restore on a fresh page load uses the same code path as after login)

## Harness shell: tabs and iframes
- Hash router (`react-router-dom`), tabs from `me/projects` (projects with a frontend), one lazy, kept-alive iframe per app at `<base path>apps/<slug>/`
- Bridge host from the new `platform-kit-web` (file dependency): init, API calls through the harness, deep-link path sync, events (theme, locale, logout, role-changed)
- nginx `/apps/` location and headers; Vite dev proxy for `/apps`; the Dockerfile copies `platform-kit-web` before `npm ci`
- vitest added (17 tests); `npm run ci` now runs them
- Verified on dev (commit dfbdfd5, manual workflow run): migration kept the URLs; `/apps/mock/` served without a token with `frame-ancestors 'self'`; non-member 403 `Project.NotMember` on `/api/mock/...`, member 200 with the role in the project token; tab, iframe, `init`, API calls through the bridge (200 as Admin, 403 on `whoami/admin` as Viewer); deep link `#/app/mock/about` and reload; theme and role-changed events; tab switching keeps the app state; unknown slug and `core` redirect home; backend-only and no-URL projects get no tab, frontend-only gets one; invalid URLs and a health URL without a backend are 400; logout removes frames and tabs; the harness page itself still answers `X-Frame-Options: DENY`
