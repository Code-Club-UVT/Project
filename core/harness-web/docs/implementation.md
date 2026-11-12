# Implementation

## What it is

A Vite + React 19 + TypeScript app styled with Tailwind v4, plus the nginx that serves it. One Docker image (`harness`) is used by every environment.

The page has a header (theme and language switches) and three cards:

- **Register**: `POST /identity/auth/register` (username, email, password, optional names).
- **Log in**: `POST /identity/auth/login`; the access token is kept in memory, the refresh token is the HttpOnly cookie.
- **Session**: shows the user and, once logged in, three buttons that call the gateway with the access token: `GET /core/users/me`, `GET /core/users/me/projects` and `GET /<mock slug>/whoami`. They show the JSON the API returns. Each goes through the gateway's token exchange, so they prove that identity mints project tokens (core's own project for the first two; the mock project's, with its role claim, for the third). The mock button only works where a `mock` project exists. Log out revokes the refresh token and forgets the access token.

Paths in the code are relative to `apiUrl` (for example `/identity/auth/login`), which is `<base path>api` deployed.

## Source layout (`src/`)

| Path | What |
|------|------|
| `config.ts` | Reads `window.__HARNESS_CONFIG__` (from `config.js`): `apiUrl`, `mockSlug` |
| `api/http.ts` | The axios instance and its interceptors; `refreshSession()` |
| `api/auth.ts`, `api/whoami.ts` | API functions; `api/problem.ts` turns errors into `{ status, code, detail }` |
| `auth/tokenStore.ts` | In-memory session (`loading`, `authenticated`, `anonymous`) with subscribe; `useSession.ts` reads it |
| `auth/useAuth.ts` | react-query mutations: register, login, logout, whoami calls |
| `auth/AuthBootstrap.tsx` | On start, tries a silent refresh from the cookie |
| `theme/ThemeProvider.tsx` | light / dark / system, saved in localStorage (`harness.theme`), applied as the `dark` class on `<html>` |
| `i18n/` | i18next, English and Romanian, saved in localStorage (`harness.lang`), browser language as the first guess |
| `components/` | Header, the three cards, shared `ui.tsx` (Card, Field, Button, ErrorNote, JsonBox) |

## Apps in iframes (`src/apps/`)

After login `useMyProjects` loads `GET /core/users/me/projects`. `visibleApps` keeps the projects with `hasFrontend` (not `core`/`identity`), sorted by name. They are the **app menu** on the home page (`AppMenu`: icon, name and role; a click goes to `/app/<slug>`). A tab exists only for an app that is open (`AppTabs`, with a close button).

- **URLs**: the harness uses history routing with clean paths, `<base path>app/<slug>/<path>` (for example `/dev/app/mock/about`), and `/` for home. `BrowserRouter` gets the base path as its basename (`routerBasename`); the nginx fallback to `index.html` makes a reload of a deep link work. The apps inside the iframes keep their hash routes; those never show in the address bar because the harness mirrors the app's path into its own URL. Old `#/app/...` links are not supported.
- **Open tabs** (`OpenTabsProvider`, pure helpers in `tabList.ts`): the list of open apps in order, each with its last path. After login the saved list is loaded (`GET /core/users/me/tabs`), filtered by the apps the user can open now, and restored. Opening an app (a menu click, a link or a deep link) adds its tab; closing removes it (and its frame) and goes home if it was the one shown. Every change is saved as the whole list (`PUT /core/users/me/tabs`) after 800 ms of quiet (`debouncedSaver`), at once when the page is hidden, and never right after a restore. The tabs are per user, not per device: the browser that saves last wins. The tabs API uses project ids; the URLs use slugs.
- **Icons** (`AppIcon`): the app's `icon.svg` at `<base path>apps/<slug>/icon.svg`, or a default icon in the harness when the app has none.
- **Frames** (`AppFrames`): one iframe per open tab, loaded the first time its tab is shown and kept mounted (hidden) afterwards, so the app keeps its state. Closing the tab removes the frame. `src` is `<base path>apps/<slug>/` (`appUrl`; the base path comes from `<base href>` through `config.basePath`: `/dev/`, `/preprod/`, `/`). The frame is registered with the host before `src` is set.
- **Bridge** (`AppBridge`): one `createAppHost` from `common/platform-kit-web/sdk`. `init` carries the user, the role (a UI hint), slug, language and theme. API calls from an app go through `fetchApi` (the shared `http` instance with the token, the single refresh and retries; `Authorization`, cookies and `X-User-*` set by the app are dropped, `Set-Cookie` is never returned). The app's path is mirrored in the address (`navigated` → `/app/<slug>/<path>`), and a tab link goes back to the last path. Events to every app: `theme` (from the `dark` class of `<html>`), `locale`, `logout` (sent before the frames are removed) and `role-changed` (`{slug, role}`, when a refetch shows another role).
- An unknown or inaccessible slug redirects to `/`; the `/api` calls stay protected by the gateway whatever the tabs show.
- nginx: `location /apps/` (`nginx/apps.conf`) forwards to the gateway (GET and HEAD, gzip) with `nginx/apps-headers.conf` (`frame-ancestors 'self'`, `X-Frame-Options: SAMEORIGIN`; the upstream's own are hidden). The harness page keeps `DENY`. Vite's dev proxy forwards `/apps` too.
- Tests (`npm test`): `visibleApps`, `appUrl`/`appRoute`/`appIconUrl`/`routerBasename`, header filtering, the tab list helpers and the debounced saver. The frames, routing and bridge in a real browser are checked end to end on dev.

## The axios instance (`api/http.ts`)

- Adds `Authorization: Bearer <access token>` to every request.
- **401**: refreshes once (`POST /identity/auth/refresh`) and repeats the request with the new token. Calls that fail at the same time share one refresh promise: a second refresh with the cookie the first one just rotated would look like token theft to identity and revoke the family. If the refresh fails, the session is cleared (the page shows the logged-out state). The auth calls set `skipAuthRefresh`, so a wrong password is just a 401.
- **Transient failures**: network errors, timeouts, 429, 502, 503 and 504 are retried up to 3 times with exponential backoff and jitter (`Retry-After` is honoured, capped at 10 s). Only GET/HEAD/OPTIONS are retried, or a request that sets `retryable: true`. The auth calls (POST) are never retried.
- react-query itself does not retry (`retry: false`): retrying is decided here, where the method is known.

## Configuration at runtime

The build has no environment values. The container's entrypoint (`nginx/entrypoint.sh`) validates and renders them at start:

| Variable | Meaning | Default |
|----------|---------|---------|
| `HARNESS_BASE_PATH` | where the app lives: `/dev/`, `/preprod/`, `/` (prod) | `/` |
| `HARNESS_API_URL` | where the browser finds `/api` | `<base path>api` (same origin) |
| `HARNESS_API_MODE` | `open` (dev) or `restricted` (preprod, prod), see below | `restricted` |
| `HARNESS_GATEWAY_URL` | the gateway on the private network | `http://gateway:8080` |
| `HARNESS_MOCK_SLUG` | slug of the mock project | `mock` |

It writes `/tmp/site/config.js` (the settings the app reads), sets `<base href>` in `index.html` (assets are relative, so one build works under any path) and chooses the nginx API config. Everything writable is in `/tmp`, so the container runs read-only.

For `npm run dev`, `public/config.js` holds local defaults (`apiUrl: "/api"`) and the Vite dev server proxies `/api` to `VITE_PROXY_TARGET` (default http://localhost:8080, the local stack in `local-deploy/`). To use the dev server instead, set `VITE_PROXY_TARGET=https://codeclub.info.uvt.ro/dev`; the proxy rewrites the refresh cookie's path and domain so it works on localhost (the real cookie path contains the environment prefix).

## nginx and the API door

Caddy sends everything of an environment to its harness container (`/dev/*`, `/preprod/*`, the rest for prod) and strips the prefix. The gateway has no route in Caddy: the harness is the only public door and forwards `/api` to the gateway over the Docker network.

| | `open` (dev) | `restricted` (preprod, prod) |
|---|---|---|
| `/api/**` to the gateway | yes | yes, with GET HEAD POST PUT PATCH DELETE OPTIONS only |
| `/health` | forwarded to the gateway | 404 |
| OpenAPI paths (`/api/openapi/...`, `/api/<slug>/openapi/...`) | forwarded | 404 |
| everything else | the app (SPA fallback to `index.html`) | the same |

Also in both: `X-Forwarded-For` from Caddy becomes the client address (`set_real_ip_from 172.16.0.0/12`) and is passed to the gateway as one value, so the gateway's per-IP rate limit sees the user; `limit_req` 20/min (burst 10) on `/api/identity/auth/`; 1 MB body limit and short header/body timeouts; security headers (`Content-Security-Policy` with `default-src 'self'`, `nosniff`, `X-Frame-Options: DENY`, `no-referrer`) on the app; hashed assets cached for a year, `index.html` and the settings files never cached. `/healthz` answers only from inside the container.

Caddy adds `Strict-Transport-Security`, `nosniff` and `no-referrer` to every response, and removes the `Server` header.

## Running everything locally

`local-deploy/` runs the whole platform, harness included, with Docker Compose (see its README). `npm run dev` then gives hot reload on top of that stack.

## Deployment

- The workflow builds the `harness` image (`core/harness-web/Dockerfile`: `npm run ci` and the build must pass, then nginx-unprivileged).
- The server (`~/codeclub-uvt`, not in git) runs it as the `harness` service of `stack-v2/docker-compose.yml`, with `HARNESS_BASE_PATH` and `HARNESS_API_MODE` from `<env>/.env`. The app services and the harness run with a read-only root filesystem and no capabilities (`no-new-privileges` is not used: on the server it stops every container from starting).
- URLs: dev `https://codeclub.info.uvt.ro/dev/` (API at `/dev/api/...`), preprod `/preprod/`, prod `/` (API at `/api/...`). The refresh cookie path is `<prefix>/api/identity/auth`, set in identity's config by `init-v2.sh`.
- Preprod still runs the old stack until it is moved to stack-v2; prod has nothing deployed yet (the root answers 502).
