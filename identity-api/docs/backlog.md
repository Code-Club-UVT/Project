# Backlog and open points

Moved from core-api with the split where they concern accounts and tokens.

## Open decisions
- **Concurrent refresh race**: two tabs refreshing at once can look like token reuse (the second call is treated as theft and revokes the family), and two truly simultaneous calls with the same token can both succeed because there is no concurrency check. Options: small grace window for the previous token, a concurrency token on `RefreshToken`, or serialize refresh on the client.
- **Account lockout** after repeated failed logins (only per-IP rate limiting at the gateway now).
- **Throttling of resend-confirmation and forgot-password** per user, so one address cannot be spammed with emails.
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
