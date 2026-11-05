# Decisions

Account and token decisions taken in core-api before the split are kept here, since identity now owns them.

| Decision | Reason |
|---|---|
| JWT access token signed with ES256 (ECDSA P-256); the public key is published as a JWKS (replaced HS256) | Many APIs now validate tokens. With a shared secret any of them could forge tokens for the others or for the gateway; with ES256 only core-api can sign and everyone else only gets the public key. Still one key and one signer |
| Private key and first-admin credentials come from configuration (environment variables when deployed); in Development an empty key generates a temporary one | Needed so a Docker container can be configured; startup fails outside Development without a key. A committed key would be a secret in git |
| Admin is seeded from `Seed:Admin` settings; no seeding if the settings are missing | Simple first-admin bootstrap that works in containers too |
| Register reports a taken username or email (409); resend-confirmation, forgot-password and login do not reveal whether an account exists | Register needs the feedback to be usable; the others must not leak accounts |
| "Email not confirmed" is returned only after the password is verified | Does not reveal which accounts exist |
| Dummy password hash check for unknown users on login | Same response time for unknown users and wrong passwords |
| Password rules: 10 to 128 characters, no forced complexity; usernames 3 to 50 characters of letters, digits, `_`, `.`, `-` | Length matters more than composition rules |
| `AuthService` (login, refresh, logout) and `AccountService` (register, confirm, resend, forgot, reset, change password) are separate services | Keeps each service's dependencies manageable |
| Only `Microsoft.AspNetCore.Authentication.JwtBearer` was added for auth | `Microsoft.Extensions.Identity.Core` (`PasswordHasher`) already ships with the ASP.NET Core framework |
| `citext` for `Username` and `Email` | Case-insensitive uniqueness with plain unique indexes |
| Login accepts username or email in one field; usernames cannot contain `@` (check constraint) | Makes the lookup unambiguous |
| JWT access token + rotating refresh token | Short-lived stateless access, revocable long-lived refresh |
| Refresh and `UserToken` values stored as SHA-256 hashes | A DB leak must not give usable tokens; a fast hash is fine for high-entropy random values |
| `FamilyId` on refresh tokens | Reuse detection revokes only the affected session family |
| `FamilyStartedAt` on refresh tokens | Absolute session cap, rotation cannot extend a session forever |
| Refresh token in an `HttpOnly` `SameSite=Strict` cookie; access token in memory | The SPA's JavaScript (and XSS) cannot read the refresh token |
| No IP or user agent stored on refresh tokens | Not needed yet; avoids storing personal data |
| `UserToken` table for email confirmation and password reset | One mechanism, hashed and single-use |
| Login blocked until the email is confirmed | Safer default |
| Identity owns credentials and every token; core-api keeps profiles and project access, keyed by the same user id | One service holds passwords and the signing key; the rest of the platform never sees a password hash |
| Own schema `identity` in the shared `platform` database, migrations history in that schema | Each service owns its tables and migrations; one database is still simple to run and back up |
| Project tokens are minted here from a read-only view of core's tables (`core.Projects`, `ProjectRoles`, `ProjectMembers`) | The signer re-checks membership at issue time without an extra HTTP call to core; the view maps only the columns it needs |
| The edge token carries no role; platform admin is the `Admin` role on the `core` project | Rights live in one place (core's project roles); every API, core included, authorizes with its project role |
| Identity is the `PassThrough` system project; the edge token is forwarded unchanged | Login and register must be anonymous, and identity validates its own edge tokens |
| Calls to core and email go through an outbox (row saved with the change, delivered by a background worker with backoff) | A user is never saved without core and email eventually hearing about it, even when they are down; the request does not wait for them |
| The outbox claims rows with `UPDATE ... FOR UPDATE SKIP LOCKED ... RETURNING` and a lease | Atomic claim without holding a transaction during HTTP calls; several instances never deliver the same row at once |
| Delivered outbox rows are deleted; failed ones are kept with `FailedAt` and `LastError` | Email bodies hold a raw token inside the link; only rows that need inspection stay |
| The outbox id is the email message id (`Idempotency-Key` header on every delivery) | Email-api skips a message id it already sent, so a retried delivery does not send twice |
| Links in emails are built here (`Frontend` settings); email-api only renders them | Identity knows the token and the SPA paths; the email service stays a pure renderer |
| Refresh cookie path is configuration (`/api/identity/auth`, with the environment prefix when deployed) | The browser sees the gateway path, not identity's own path |
| `IDesignTimeDbContextFactory` for migrations | `dotnet ef` and the migration bundle never start the app (which needs a signing key and the outbox settings) |
