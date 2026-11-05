# Implementation

`mock-api` is a minimal project API used to test a deployment end to end. Slug: `mock`.

## Endpoints

| Method | Path | Access | Returns |
|--------|------|--------|---------|
| GET | `/api/whoami` | any member of the project | user id, username, role, project slug, correlation id, client IP |
| GET | `/api/whoami/admin` | role `Admin` in the project | a message; 403 for other roles |
| GET | `/health` | anonymous | health, for the gateway and Docker |

Through the gateway these are `/api/mock/whoami` and so on (publicly `/<env>/api/mock/whoami`).

## Configuration

| Setting | Value |
|---------|-------|
| `PlatformAuth__Audience` | `mock` (default in `appsettings.json`) |
| `PlatformAuth__Issuer` | `identity` |
| `PlatformAuth__JwksUrl` | `http://identity-api:8080/.well-known/jwks.json` in the deployments |
| `PlatformAuth__AllowInsecureJwksUrl` | `true` on the private Docker network |

The client IP is read from `X-Forwarded-For`, trusted only from the private Docker range (172.16.0.0/12), so `clientIp` in `/api/whoami` shows the real address of the browser when the whole chain works.

## Registering it as a project

Once per environment, as a core admin (Admin role on the `core` project): create a project with slug `mock`, base URL `http://mock-api:8080/api` and health URL `http://mock-api:8080/health` (see `core-api/docs/implementation.md`, endpoint `/api/core/projects`), then add yourself as a member, or create a role and set it as the project's default role so every newly confirmed user gets it.
