# Backlog and open points

Moved from core-api with the split where they concern accounts and tokens.

## Open decisions
- **Refresh concurrency check**: two truly simultaneous calls with the same unrotated token can both succeed because there is no concurrency token on `RefreshToken` (the grace window only covers a call that arrives after the first rotated). Harmless so far (both get valid tokens); add a concurrency token if it matters.
- **Throttle state is in memory**: counts of failed logins and emails reset on restart and are per instance. Move to the database or a shared cache when identity runs in more than one instance.
- **Tests**: identity has no test project, so the limiter and the refresh grace window were checked on dev only. A test project (xunit, like `gateway/Gateway.Tests`) was postponed by the owner.
- **Duplicate registration race**: two simultaneous registrations of the same username pass the existence check and the second hits the unique index (500). Map the unique violation to 409.
- **Deactivated users** keep a valid access token for up to 15 minutes. There is no endpoint to deactivate a user yet (it would live here, called by core's admin side through an internal endpoint).
- **Instant access-token revocation**: not possible with stateless JWTs; if needed, a token-version or `jti` check against a cache.
- **Key rotation**: the JWKS holds one key. For rotation publish old and new keys together and sign with the new one; validators pick by `kid` (platform-kit already handles an unknown `kid`).
- **Email change**: not supported. It needs a confirmation flow; core's upsert already refreshes the username and email copy.
- Password hasher iteration count is the framework default; consider raising it.
- **Swagger**: the Bearer requirement is applied to every operation in the document, including anonymous ones.
- **Outbox**: no cleanup or alert for failed rows (`FailedAt` set) and no metrics. An alert on "given up" log lines would help.
- **Deployment of SPA and API**: `SameSite=Strict` needs them to be same-site. On unrelated domains switch the cookie to `SameSite=None` and add CSRF protection.

## Future / maintenance
- **Cleanup job** for expired or revoked `RefreshToken` rows and expired or used `UserToken` rows.
- Sessions list for the user ("active devices"); would need IP and user agent on `RefreshToken`.
- Tests: none yet.
- OpenAPI schema in every environment (see core-api/docs/backlog.md and platform-kit/docs/backlog.md).
