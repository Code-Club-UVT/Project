# Changelog

## Step 1: the harness
- Created `harness-web`: Vite, React 19, TypeScript, Tailwind v4, react-query, axios, i18next (English and Romanian), light/dark/system theme, Biome
- Test page: register, log in, log out, and `whoami` calls through the gateway (core profile, core projects, mock project)
- axios instance with the access token, one shared refresh on 401 and retries for transient failures
- Dockerfile (Biome and type check, build, nginx-unprivileged) and nginx config: serves the app, forwards `/api` to the gateway, `open` and `restricted` modes, CSP and other headers, client IP handling; settings rendered at start from environment variables
- Workflow builds the `harness` image
- Server (not in git): `harness` service in `stack-v2/docker-compose.yml`; Caddy routes each environment to its harness, with security headers, and no longer has a route to any gateway; read-only, capability-free containers for all app services; `init-v2.sh` and the identity template know the prod root path and the harness variables
- Not done: preprod was not moved to stack-v2 (it would stay down until the next merge to main; see backlog)
