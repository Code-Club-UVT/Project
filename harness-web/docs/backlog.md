# Backlog and open points

## Next steps
- Move preprod to stack-v2 (`scripts/init-v2.sh preprod`, fresh database), then switch its Caddy block to `preprod-harness` (the comment in the Caddyfile has the exact change). Prod gets the same at its first release; nothing is deployed there yet.
- Pages for the emailed links: confirm email, reset password (identity builds them from `Frontend:BaseUrl`, which is now `<domain>/<prefix>`), plus forgot password and change password. Needs a router.
- Show a project/role overview on the home tab (the tabs already come from `me/projects`).
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
