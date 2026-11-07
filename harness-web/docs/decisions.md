# Decisions

- **The harness is the only public door; the gateway has no route in Caddy.** The browser app and its API share one origin: no CORS, and the `SameSite=Strict` refresh cookie works as designed (a separate API host would make it cross-site and need CSRF protection). One public surface to harden. The cost is one nginx hop. If third parties ever need the APIs, a separate hostname pointing at the gateway is a small Caddy block.
- **Same path and same image in every environment.** Browser and Postman both use `/<prefix>/api/...`. Dev differs only by `HARNESS_API_MODE=open` (dev also exposes `/health` and the OpenAPI paths), so dev exercises the same hop as prod.
- **Runtime configuration, not build arguments.** Images are built once per commit and promoted by tag, so the base path and API URL are rendered by the container at start (`config.js`, `<base href>`). Assets are relative.
- **Access token in memory only.** A reload restores the session through the refresh cookie; nothing a script can read survives a reload (XSS cannot steal a long-lived credential). The strict CSP backs this up.
- **One shared refresh.** Concurrent 401s wait for one refresh call (see identity's backlog: concurrent refresh looks like reuse).
- **Retries only where safe.** The axios interceptor retries idempotent requests; react-query does not retry.
- **Biome only** for linting and formatting; `npm run ci` runs in the Docker build, so a lint or type error fails the image.
- **No router, no tests, no generated API clients yet** (by choice; tests for the APIs and the app come later).
- **Read-only containers.** The harness and the app services run read-only, without capabilities and with `no-new-privileges`; they write only to a tmpfs `/tmp`.
