# Backlog and open points

## Next steps
- Move preprod to stack-v2 (`scripts/init-v2.sh preprod`, fresh database), then switch its Caddy block to `preprod-harness` (the comment in the Caddyfile has the exact change). Prod gets the same at its first release; nothing is deployed there yet.
- Pages for the emailed links: confirm email, reset password (identity builds them from `Frontend:BaseUrl`, which is now `<domain>/<prefix>`), plus forgot password and change password. Needs a router.
- Tabs are saved per user, not per device: two browsers overwrite each other's list (the last one to change tabs wins). Per-device lists or merging would need a device id.
- Admin screens to register projects and their URLs (today a core admin calls the API by hand).
- Tests of the frames, routing and `AppBridge` need a DOM test environment (jsdom); today they are checked end to end on dev. The shared library's own tests are not run by any image build (`npm test` in `platform-kit-web`).
- Tests for the interceptors (refresh sharing, retries): vitest is now available.

## Open decisions
- **Public registration** is open in every environment (identity). Decide whether prod needs invitations or admin approval.
- **OpenAPI paths** are 404 in restricted mode until it is decided how the schemas are published (see platform-kit and gateway backlogs).
- **Third-party API access** (other servers calling the APIs): would need its own hostname for the gateway. Not needed while the harness is the only client.
- **Server hardening outside this change**: firewall (the README says it is off and Docker bypasses ufw), the dev database port (5432 is published with only `pg_hba.conf` and a password), scheduled backups, SSH hardening, Dependabot or `npm audit`, base images pinned by digest.
- **Biome and Tailwind class order**: `useSortedClasses` is not enabled.
- **Several harness replicas or WebSockets/streaming** through the proxy: not tested.
- `nginx` access logs go to stdout with the default format; a structured format and the correlation id would help.

## Found while verifying the iframe tabs on dev
- A project deleted or removed from the user's list keeps its tab until the next refetch of `me/projects` (window focus); the frame stays until then.
- A new membership or role change takes up to 30 s to apply at the gateway (access cache, `Routing` settings), so a just-granted user may see 403 for a moment.
- Any path the gateway has no route for answers 401 to an anonymous caller (404 when authenticated), so a missing project and a missing frontend look alike from outside.
- The mock project on dev is registered (backend `mock-api`, frontend `mock-web`, roles `Admin` and `Viewer`, the dev admin is an `Admin`).
