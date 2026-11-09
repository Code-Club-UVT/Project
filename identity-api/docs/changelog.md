# Changelog

## Tests
- New `Identity.Tests` (114 tests, in-memory fakes of the repositories): `AttemptLimiter`; login (success, the same error for unknown, wrong and disabled, the unconfirmed order, rehash, the throttle and its window); refresh (rotation, absolute lifetime, expiry, reuse of a revoked token ends the family, the grace window in all its cases); logout; register, confirm, resend, forgot, reset and change password with the email limits; `TokenService`, `SigningKeyProvider` and `ProjectTokenService`; the outbox and the dispatcher (delivery, backoff, give-up); `AuthController` cookie handling; the authorization attributes of every controller
- Found while writing them and fixed: the limiter swept its whole table on every call once it held more than 10,000 entries (now once a minute), and kept login entries for an hour instead of their own 15-minute window (each entry now carries its window)

## Security: throttling and refresh race
- New `IAttemptLimiter` / `AttemptLimiter` (in memory) and `Throttle` settings: failed logins per identifier (10 in 15 minutes, then 429 `Auth.TooManyAttempts`), emails per address (one per 2 minutes, five per hour; over the limit the call answers success and sends nothing). Applied to login, register (counts the first email), resend-confirmation and forgot-password
- Refresh: a token rotated at most `RefreshToken:ReuseGraceSeconds` (10) ago gets a new access token and no cookie instead of revoking the family; `AuthSession` carries an optional refresh token and `AuthController` sets the cookie only when there is one
- Uses the new `ErrorType.TooManyRequests` of platform-kit (429)
- Verified on dev (commit 887ca03, manual workflow run): ten wrong logins for a made-up identifier gave 401 `Auth.InvalidCredentials`, the next ones 429 `Auth.TooManyAttempts` (from identity, not the gateway), another identifier was not blocked; six refreshes with the same cookie, sent together or 25 and 60 ms apart, all gave 200 and the session stayed valid; the log showed two grace uses ("access token only") and no family revocation. Not tested live: refresh outside the window (the HttpOnly cookie cannot be replayed from the page), and the email limits (an unknown address returns before the limiter; a real address would send a real email)
- Found in a review of the code (2026-11-09); the findings that are not fixed are in the backlogs of harness-web, gateway and core-api

## Step 1: identity-api (service split)
- Created from core-api's account and token code: `AuthService`, `AccountService`, `TokenService`, `SigningKeyProvider`, `ProjectTokenService`, the auth, JWKS and internal project-token controllers, and the user, refresh-token and user-token entities. Behaviour and security rules are unchanged (see auth-design.md); the history before this step is in core-api/docs/changelog.md
- Changed: issuer `identity`; no role in the edge token; `User` has no role and no profile (names go to core-api); change password moved to `POST /api/account/change-password`; the refresh cookie path is `/api/identity/auth`
- New: own schema `identity` with a fresh `InitialCreate` migration; `CoreReadDbContext` to mint project tokens from core's tables; an outbox (`OutboxMessages`, `IOutbox`, `OutboxDispatcher`) that replaces inline email sending and tells core about new and confirmed users; `AdminSeeder` queues both events
- Uses platform-kit for results, ProblemDetails, request context, health and the internal key scheme (`InternalAuth:Callers:gateway`)
- Dockerfile with `api` and `migrate` targets, `IdentityApi.http`, `CLAUDE.md`, this `docs/` folder
- The migration bundle names its context (`--context PostgresDbContext`): with `CoreReadDbContext` there are two (found by the first dev deploy)
- Verified on the dev server (see implementation.md)
