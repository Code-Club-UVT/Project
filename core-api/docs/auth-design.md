# Authentication design

Status: **implemented and verified end to end** against a local PostgreSQL in Docker (all flows below: register, confirm, login, refresh rotation and reuse detection, logout, forgot/reset, change password, resend confirmation, admin seeding, admin list). Open points are in [backlog.md](backlog.md).

Client: a browser SPA (React + TypeScript).

## Tokens

- **Access token**: JWT (HS256), 15 minutes, sent as `Authorization: Bearer`. Stateless, so it cannot be revoked early. The SPA keeps it in memory only. Claims: `sub` (user id), `jti`, `email`, `username`, `role`.
- **Refresh token**: opaque random value (32 bytes from a cryptographic RNG, base64url), 7 days, capped by an absolute lifetime of 30 days since login. Stored in the DB as a SHA-256 hash (hex). Delivered only in an `HttpOnly; SameSite=Strict` cookie (`Secure` unless turned off for local HTTP), `Path=/api/auth`. It is never in a response body.
- **UserToken**: opaque random value in an emailed link. Hashed in the DB, single-use. Password reset 1 hour, email confirmation 24 hours.

All lifetimes, the cookie name and the `Secure` flag are configuration (`appsettings.json`), not columns.

## Endpoints

`/api/auth` (anonymous, rate limited to 10 requests per minute per IP):

| Endpoint | Body | Result |
|---|---|---|
| `POST register` | username, email, password, firstName?, lastName? | 200 user summary; 409 if username or email is taken |
| `POST confirm-email` | token | 204; 400 if the token is invalid, expired or used |
| `POST resend-confirmation` | email | 204 always |
| `POST login` | identifier (username or email), password | 200 `{ accessToken, expiresAt, user }` + refresh cookie |
| `POST refresh` | none (cookie) | 200 as login, rotated cookie; 401 and the cookie is cleared if invalid |
| `POST logout` | none (cookie) | 204, token revoked, cookie cleared |
| `POST forgot-password` | email | 204 always |
| `POST reset-password` | token, newPassword | 204; revokes all the user's refresh tokens |

`/api/users` (authenticated):

| Endpoint | Access | Result |
|---|---|---|
| `GET me` | any user | current user and profile |
| `PUT me` | any user | updates first and last name |
| `POST me/change-password` | any user | 204; checks the current password, revokes all refresh tokens |
| `GET ?page=&pageSize=` | Admin | paged user list (page size capped at 100) |

Every endpoint requires authentication by default (fallback policy); only the auth endpoints and the OpenAPI document are anonymous.

## Flows

- **Register**: validate, check username and email are free, hash the password, insert `User` (role `User`, not confirmed) and `UserProfile`, create an `EmailConfirmation` token, commit, send the link.
- **Confirm email**: find the token by hash and type, check not expired and not used, set `EmailConfirmed` and `UsedAt`.
- **Login**: `identifier` containing `@` is an email, otherwise a username. Unknown user, wrong password and inactive account all return the same 401. A dummy hash check runs for unknown users so timing does not reveal them. "Email not confirmed" (403) is returned only after the password is verified. Success creates a refresh token with a new `FamilyId` and returns the access token.
- **Refresh**: find the token by hash. Active and not expired: rotate (old token gets `RevokedAt` and `ReplacedByTokenId`, the new one keeps the `FamilyId` and `FamilyStartedAt`, and `ExpiresAt = min(now + 7 days, FamilyStartedAt + 30 days)`). A revoked token coming back is treated as theft: the whole family is revoked and the call fails. Inactive or unconfirmed users cannot refresh.
- **Logout**: revoke the current refresh token and clear the cookie; always succeeds.
- **Forgot / reset password**: forgot creates a `PasswordReset` token (older unused ones are marked used) and sends the link, always returning 204. Reset sets the new hash, marks the token used and revokes all refresh tokens.
- **Change password**: verifies the current password (wrong gives 400, not 401), sets the new hash, revokes all refresh tokens. The current access token stays valid until it expires.

## Security rules

- Passwords are hashed with ASP.NET Core `PasswordHasher` (PBKDF2), 10 to 128 characters, no forced complexity. The hash is upgraded on login when the hasher asks for it. Passwords and tokens are never stored raw or logged.
- Exception: in Development the email sender writes the link, which contains a raw token, to the log. It is registered only in Development; any other environment fails at startup until a real `IEmailSender` is registered.
- JWT validation: signature, issuer, audience, lifetime, pinned algorithm (HS256), 30 seconds of clock skew. The signing key must be at least 32 characters, and outside Development it must not be the `CHANGE-ME...` placeholder (startup fails otherwise).
- No sensitive data in the JWT (it is signed, not encrypted).
- Register reports a taken username or email (needed for the user); resend-confirmation, forgot-password and login do not reveal whether an account exists.
- CORS: only the origin of `Frontend:BaseUrl`, with credentials.
- Roles come from the `role` claim; `Admin` and `User` only. A deactivated user's access token stays valid until it expires (at most 15 minutes); they cannot log in or refresh.

## Configuration

| Section | Keys |
|---|---|
| `Jwt` | `Issuer`, `Audience`, `SigningKey`, `AccessTokenMinutes` |
| `RefreshToken` | `Days`, `AbsoluteDays`, `CookieName`, `CookieSecure` |
| `Auth` | `EmailConfirmationHours`, `PasswordResetHours` |
| `Frontend` | `BaseUrl`, `ConfirmEmailPath`, `ResetPasswordPath` (used for CORS and the emailed links) |
| `Seed:Admin` | `Username`, `Email`, `Password` (first admin; created at startup only if all three are set and the user does not exist) |

Any key can be overridden with an environment variable using `__` as the separator, for example `Jwt__SigningKey` or `Seed__Admin__Password`. This is how a Docker container gets its real values.
