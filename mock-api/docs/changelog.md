# Changelog

## Step 1: first version

- Project created with `GET /api/whoami`, `GET /api/whoami/admin` and `/health`, Dockerfile and docs.

## Step 2: service split
- References `platform-kit` (was `gateway-auth`); settings section `PlatformAuth`, issuer `identity`, JWKS from identity-api
