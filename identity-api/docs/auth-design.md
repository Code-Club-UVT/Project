# Authentication design

Moved here from core-api when the services were split; identity is now the only service that holds credentials and signs tokens.

Client: a browser SPA (React + TypeScript), not built yet. It talks only to the gateway.

## Tokens

- **Access token** (the "edge token"): JWT signed with ES256, 15 minutes, issuer `identity`, audience `gateway`, sent as `Authorization: Bearer`. Stateless, so it cannot be revoked early. The SPA keeps it in memory only. Claims: `sub` (user id), `jti`, `email`, `username`. **No role**: rights are per project, and platform admin is the `Admin` role on the `core` project.
- **Project token**: JWT signed with the same key, 5 minutes, audience = the project slug. Claims: `sub`, `jti`, `username`, `role` (the user's role **in that project**). Minted by identity only, on the gateway's request (`POST /internal/project-tokens`), and used only between the gateway and that project's API (core-api included). The audiences keep the kinds apart: an API rejects the edge token and every other project's token.
- **Refresh token**: opaque random value (32 bytes from a cryptographic RNG, base64url), 7 days, capped by an absolute lifetime of 30 days since login. Stored as a SHA-256 hash (hex). Delivered only in an `HttpOnly; SameSite=Strict` cookie (`Secure` unless turned off for local HTTP), `Path` = `RefreshToken:CookiePath` (`/api/identity/auth`, the public path behind the gateway; deployed it includes the environment prefix, for example `/dev/api/identity/auth`). Never in a response body.
- **UserToken**: opaque random value in an emailed link. Hashed in the DB, single-use. Password reset 1 hour, email confirmation 24 hours.

All lifetimes, the cookie name and the `Secure` flag are configuration, not columns. The signing key is one ECDSA P-256 key (`Jwt:PrivateKeyPem`); its public half is published at `/.well-known/jwks.json` (not routed by the gateway; services fetch it on the private network).

## Endpoints

`/api/auth` (anonymous; public path `/api/identity/auth`; the gateway rate limits it per IP):

| Endpoint | Body | Result |
|---|---|---|
| `POST register` | username, email, password, firstName?, lastName? | 200 account summary; 409 if username or email is taken |
| `POST confirm-email` | token | 204; 400 if the token is invalid, expired or used |
| `POST resend-confirmation` | email | 204 always |
| `POST login` | identifier (username or email), password | 200 `{ accessToken, expiresAt, user }` + refresh cookie; 429 `Auth.TooManyAttempts` after too many failures for that identifier |
| `POST refresh` | none (cookie) | 200 as login, rotated cookie (no cookie when the call lost a race, see Refresh below); 401 and the cookie is cleared if invalid |
| `POST logout` | none (cookie) | 204, token revoked, cookie cleared |
| `POST forgot-password` | email | 204 always |
| `POST reset-password` | token, newPassword | 204; revokes all the user's refresh tokens |

`/api/account` (edge token required; public path `/api/identity/account`):

| Endpoint | Result |
|---|---|
| `POST change-password` | 204; checks the current password (wrong gives 400), revokes all refresh tokens |

`user` in the login response is `{ id, username, email, emailConfirmed, createdAt }`. The profile (names) and project access come from core-api (`GET /api/core/users/me`, `GET /api/core/users/me/projects`).

Internal (never routed by the gateway):

| Endpoint | Caller | Result |
|---|---|---|
| `POST /internal/project-tokens` `{ userId, slug }` | gateway (`X-Internal-Key`) | 200 `{ token, expiresAt, roleName }`; 404 unknown/inactive user or project (or a PassThrough project); 403 not a member |
| `GET /.well-known/jwks.json` | anyone on the private network | the public keys |

## Flows

- **Register**: validate, check username and email are free, hash the password, insert `User` (not confirmed) and an `EmailConfirmation` token, and in the same transaction queue two outbox messages: `user-registered` to core (id, username, email, first and last name, so core creates the profile) and an `email-confirmation` email with the link. The name is not stored in identity.
- **Confirm email**: find the token by hash and type, check not expired and not used, set `EmailConfirmed` and `UsedAt`, and queue `user-confirmed` to core (core then grants the projects' default roles).
- **Login**: `identifier` containing `@` is an email, otherwise a username. Unknown user, wrong password and inactive account all return the same 401. A dummy hash check runs for unknown users so timing does not reveal them. "Email not confirmed" (403) is returned only after the password is verified. Success creates a refresh token with a new `FamilyId` and returns the access token. **Throttle**: failures are counted per identifier (lowercased, known or unknown alike, in memory); after `Throttle:LoginFailures` (10) in `LoginWindowMinutes` (15) the login answers 429 `Auth.TooManyAttempts` before looking at the password, until the oldest failure leaves the window. A refused attempt is not counted. A successful login clears the count. This lets a stranger block someone's login but not enter; the password reset by email is not blocked.
- **Refresh**: find the token by hash. Active and not expired: rotate (old token gets `RevokedAt` and `ReplacedByTokenId`, the new one keeps the `FamilyId` and `FamilyStartedAt`, and `ExpiresAt = min(now + 7 days, FamilyStartedAt + 30 days)`). A revoked token coming back is treated as theft: the whole family is revoked and the call fails, **except** a token that was rotated at most `RefreshToken:ReuseGraceSeconds` (10) ago whose replacement is still valid (two tabs refreshing at once): that call gets a new access token and no refresh token, and sets no cookie, so the cookie of the first response stays. Tokens revoked by logout or a password change have no replacement, so they never qualify. A token replayed inside the window by a thief gets one access token (15 minutes), never a refresh token. Inactive or unconfirmed users cannot refresh.
- **Logout**: revoke the current refresh token and clear the cookie; always succeeds.
- **Forgot / reset password**: forgot creates a `PasswordReset` token (older unused ones are marked used) and queues the `password-reset` email, always returning 204. **Throttle**: emails (confirmation, resend, reset) to one address wait `Throttle:EmailMinIntervalSeconds` (120) after the previous one and stop at `EmailsPerHour` (5); over the limit the call still answers success and sends nothing (the earlier link stays valid). Reset sets the new hash, marks the token used and revokes all refresh tokens.
- **Change password**: verifies the current password, sets the new hash, revokes all refresh tokens. The current access token stays valid until it expires.
- **Project token** (gateway): the user must exist and be active, the project must be active and `Exchange`, and the user must be a member; the role name is read from core's tables (read-only, schema `core`).
- **First admin**: `Seed:Admin` creates the account already confirmed and queues `user-registered` + `user-confirmed`. Core grants `Admin` on the core project because its `Seed:AdminEmail` is the same address.

## Security rules

- Passwords are hashed with ASP.NET Core `PasswordHasher` (PBKDF2), 10 to 128 characters, no forced complexity. The hash is upgraded on login when the hasher asks for it. Passwords and tokens are never stored raw or logged.
- Outbox rows for emails hold the link, and so a raw token, until they are delivered; delivered rows are deleted. Failed rows (after the last attempt) are kept for inspection: treat the table as sensitive.
- JWT validation of identity's own edge tokens (for `change-password`): signature, issuer, audience `gateway`, lifetime, pinned algorithm (ES256), 30 seconds of clock skew. The private key must be an ECDSA P-256 key and is required outside Development (startup fails otherwise); in Development an empty key generates a temporary one.
- Internal endpoints use the `InternalKey` scheme from platform-kit (`InternalAuth:Callers:gateway`). A bearer token does not work there, and the key does not work on `/api`.
- No sensitive data in the JWT (it is signed, not encrypted).
- Register reports a taken username or email (needed for the user); resend-confirmation, forgot-password and login do not reveal whether an account exists.
- CORS and rate limiting are at the gateway. Identity must only be reachable through it (private network).
- A deactivated user's access token stays valid until it expires (at most 15 minutes); they cannot log in, refresh or get project tokens, and the gateway refuses them after its access cache time.

## Configuration

| Section | Keys |
|---|---|
| `Jwt` | `Issuer` (`identity`), `Audience` (`gateway`), `PrivateKeyPem`, `AccessTokenMinutes`, `ProjectTokenMinutes` |
| `InternalAuth:Callers` | `gateway`: the key the gateway sends |
| `Services` | `Core:Url`, `Core:Key`, `Email:Url`, `Email:Key` (where the outbox delivers, and the key each expects), `TimeoutSeconds` |
| `Outbox` | `PollSeconds` (2), `BatchSize` (20), `MaxAttempts` (30), `FirstBackoffSeconds` (2), `MaxBackoffSeconds` (600), `LeaseSeconds` (60) |
| `RefreshToken` | `Days`, `AbsoluteDays`, `CookieName`, `CookiePath`, `CookieSecure`, `ReuseGraceSeconds` (10; 0 turns the race tolerance off) |
| `Throttle` | `LoginFailures` (10), `LoginWindowMinutes` (15), `EmailMinIntervalSeconds` (120), `EmailsPerHour` (5) |
| `Auth` | `EmailConfirmationHours`, `PasswordResetHours` |
| `Frontend` | `BaseUrl`, `ConfirmEmailPath`, `ResetPasswordPath` (used for the emailed links) |
| `Seed:Admin` | `Username`, `Email`, `Password` (first admin; created at startup only if all three are set and the user does not exist) |

Any key can be overridden with an environment variable using `__` as the separator, for example `Jwt__PrivateKeyPem`.
