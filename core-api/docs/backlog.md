# Backlog and open points

## Next steps
- Dockerfile for the API and the API service in `docker-compose.yml` (needed for the Ubuntu server deployment; check that `Jwt__SigningKey`, `Seed__Admin__*`, the connection string and `Smtp__*` are supplied by environment variables).
- Do not leave pgAdmin or the Postgres port exposed on the server: the `tools` profile keeps pgAdmin off by default; keep the database port bound to localhost or remove the published port when the API runs in the same Compose network.
- Optional extra tests: token expiry in the database (shorten the lifetimes in configuration and wait), and an automated test project so the scripted run does not have to be done by hand.
- Before deploying: set `Jwt__SigningKey` (32+ random characters), `Seed__Admin__*`, `RefreshToken__CookieSecure=true` (default), `Frontend__BaseUrl`, `Smtp__*` and the connection string through environment variables, and rotate anything that was committed as a placeholder.
- **Gateway**: built (`gateway/`). Core-api must not be reachable except through it (private network): it has no rate limiting or CORS of its own now, and the refresh cookie path is `/api/core/auth`, so the SPA must call `<gateway>/api/core/...`. Direct calls from a browser to core-api no longer work.
- Core-api `Dockerfile` must build with the repo root as context if it ever references `gateway-auth` (APIs that use it do).
- Forwarded headers: when the gateway sits behind another reverse proxy (nginx), configure `UseForwardedHeaders` there with the trusted proxy so client IPs are right.

## Open decisions
- **MongoDB and Neo4j**: which data goes in each, and the driver packages (`MongoDB.Driver`, `Neo4j.Driver`); each gets its own folder under `Data/` and its own connection string. Decide where their models live (`Entities/` is relational-only for now).
- **Deployment of SPA and API**: `SameSite=Strict` needs them to be same-site (subdomains of one domain or one domain behind a reverse proxy). If they end up on unrelated domains, switch the cookie to `SameSite=None` and add explicit CSRF protection.
- **Email delivery for production**: Gmail with an app password is for testing only (sending limits, may be flagged). Pick a transactional provider or a domain mailbox with SPF/DKIM before deploying.
- **Background email queue** (`Channel<T>` plus a hosted service) with retries, so a slow or failing SMTP server does not fail the request (today a failure returns 500 after the data is saved).
- **Connection string and secrets**: the committed `appsettings.Development.json` holds placeholder local credentials. Move real credentials and the JWT signing key to user-secrets or environment variables.
- **Concurrent refresh race**: two tabs refreshing at once can look like token reuse (the second call is treated as theft and revokes the family), and two truly simultaneous calls with the same token can both succeed because there is no concurrency check. Options: small grace window for the previous token, a concurrency token on `RefreshToken`, or serialize refresh on the client.
- **Account lockout** after repeated failed logins (only per-IP rate limiting exists now).
- **Throttling of resend-confirmation and forgot-password** per user (only per-IP rate limiting exists now), so one address cannot be spammed with emails.
- **Duplicate registration race**: two simultaneous registrations of the same username pass the existence check and the second hits the unique index, which currently surfaces as a 500. Map the unique-violation to a 409.
- **Deactivated users** keep a valid access token for up to 15 minutes. Acceptable for now.
- **Swagger**: the Bearer requirement is applied to every operation in the document, including anonymous ones.
- Password hasher iteration count is the framework default; consider raising it.
- **Instant access-token revocation**: not possible with stateless JWTs; if needed, add a token-version or `jti` check against a cache. With the gateway, project access changes and deactivated users take effect after the gateway's cache TTL (about 30 seconds) for `Exchange` projects.
- **Project URLs and DNS**: URL validation does not resolve host names, so a name that resolves to a loopback or metadata address (or changes after validation, DNS rebinding) passes. Fix on the gateway side with an egress allow-list or a resolving handler.
- **Duplicate project slug / role name race**: like the duplicate registration race, a simultaneous create hits the unique index and surfaces as a 500; map it to 409.
- **Key rotation**: the JWKS holds one key. For rotation publish old and new keys together, sign with the new one, and let the validators pick by `kid`.
- **Audit log** of access changes (who granted or revoked what, when); `GrantedByUserId` only records the latest grant.
- **Project requests and expiring grants** (users asking for access, `ExpiresAt` on a membership).
- **Monitoring**: a status view of project health (from the gateway's probes), Uptime Kuma or OpenTelemetry.
- `CoreApi.http` has no requests for the project endpoints yet.
- **SMTP password in `appsettings.Development.json`**: a real Gmail app password is in a tracked file, against the `CHANGE-ME` rule in `CLAUDE.md`. Before committing: move it to user-secrets or an environment variable, rotate the app password, and leave a placeholder with `Smtp:Enabled=false` in the file (open from the code review).
- **Concurrent grant**: two simultaneous grants of the same user to the same project give a 500 from the primary key instead of 409 `ProjectMember.AlreadyMember` (same race as the slug and role name ones above; open from the code review).
- **Duplicated helpers**: `CheckProjectAsync` is in both `ProjectRoleService` and `ProjectMemberService`, and `NullIfBlank` in three services; move them to a shared place (open from the code review).
- **PassThrough in the data**: add a CHECK constraint so only a system project can be `PassThrough` (see the gateway backlog).

## Future / maintenance
- **Shared email templates / email service**: when a second API needs emails, move `EmailTemplates` to a shared project, or to an email service. Leaning towards the service owning the templates: callers send `{ template, to, data }` (not HTML), so branding stays in one place and callers cannot send arbitrary content; delivery through a queue. Sending a ready-made body is possible as an escape hatch but needs the layout shared anyway.
- **Queryable logs in a database**, for every action. Two separate things: (1) application logs (errors, warnings), for example a Serilog sink to a database; (2) an audit/action log (who did what, when, status, duration), a good fit for MongoDB. Needs a hook point such as a middleware or action filter, plus a correlation/request id. Using `ILogger` everywhere now keeps both options open. Decide the store and the fields before building.
- Test project: none yet. The repositories need a test database (for example Postgres in Docker) to be tested for real.
- **Cleanup job** for expired or revoked `RefreshToken` rows and expired or used `UserToken` rows (they accumulate).
- Sessions list for the user ("active devices"); would need IP and user agent back on `RefreshToken` (dropped for now).
- More `UserProfile` fields (display name, avatar, bio, ...).
- Rate limiting and account lockout configuration.
- Tests (none yet).
