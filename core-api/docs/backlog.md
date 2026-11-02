# Backlog and open points

## Next steps
- Dockerfile for the API and the API service in `docker-compose.yml` (needed for the Ubuntu server deployment; check that `Jwt__SigningKey`, `Seed__Admin__*`, the connection string and an email sender are supplied by environment variables).
- Do not leave pgAdmin or the Postgres port exposed on the server: the `tools` profile keeps pgAdmin off by default; keep the database port bound to localhost or remove the published port when the API runs in the same Compose network.
- Optional extra tests: token expiry in the database (shorten the lifetimes in configuration and wait), and an automated test project so the scripted run does not have to be done by hand.
- Real `IEmailSender` (SMTP or a provider). Until then the app only starts in Development.
- Before deploying: set `Jwt__SigningKey` (32+ random characters), `Seed__Admin__*`, `RefreshToken__CookieSecure=true` (default), `Frontend__BaseUrl` and the connection string through environment variables, and rotate anything that was committed as a placeholder.
- Forwarded headers: behind a reverse proxy or Docker network the rate limiter sees the proxy's IP for everyone. Configure `UseForwardedHeaders` with the trusted proxy.

## Open decisions
- **MongoDB and Neo4j**: which data goes in each, and the driver packages (`MongoDB.Driver`, `Neo4j.Driver`); each gets its own folder under `Data/` and its own connection string. Decide where their models live (`Entities/` is relational-only for now).
- **Deployment of SPA and API**: `SameSite=Strict` needs them to be same-site (subdomains of one domain or one domain behind a reverse proxy). If they end up on unrelated domains, switch the cookie to `SameSite=None` and add explicit CSRF protection.
- **Real email provider** (SMTP or a service) to replace the dev sender.
- **Connection string and secrets**: the committed `appsettings.Development.json` holds placeholder local credentials. Move real credentials and the JWT signing key to user-secrets or environment variables.
- **Concurrent refresh race**: two tabs refreshing at once can look like token reuse (the second call is treated as theft and revokes the family), and two truly simultaneous calls with the same token can both succeed because there is no concurrency check. Options: small grace window for the previous token, a concurrency token on `RefreshToken`, or serialize refresh on the client.
- **Account lockout** after repeated failed logins (only per-IP rate limiting exists now).
- **Throttling of resend-confirmation and forgot-password** per user (only per-IP rate limiting exists now), so one address cannot be spammed with emails.
- **Duplicate registration race**: two simultaneous registrations of the same username pass the existence check and the second hits the unique index, which currently surfaces as a 500. Map the unique-violation to a 409.
- **Deactivated users** keep a valid access token for up to 15 minutes. Acceptable for now.
- **Swagger**: the Bearer requirement is applied to every operation in the document, including anonymous ones.
- Password hasher iteration count is the framework default; consider raising it.
- **Instant access-token revocation**: not possible with stateless JWTs; if needed, add a token-version or `jti` check against a cache.

## Future / maintenance
- **Queryable logs in a database**, for every action. Two separate things: (1) application logs (errors, warnings), for example a Serilog sink to a database; (2) an audit/action log (who did what, when, status, duration), a good fit for MongoDB. Needs a hook point such as a middleware or action filter, plus a correlation/request id. Using `ILogger` everywhere now keeps both options open. Decide the store and the fields before building.
- Test project: none yet. The repositories need a test database (for example Postgres in Docker) to be tested for real.
- **Cleanup job** for expired or revoked `RefreshToken` rows and expired or used `UserToken` rows (they accumulate).
- Sessions list for the user ("active devices"); would need IP and user agent back on `RefreshToken` (dropped for now).
- More `UserProfile` fields (display name, avatar, bio, ...).
- Rate limiting and account lockout configuration.
- Tests (none yet).
