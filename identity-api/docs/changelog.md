# Changelog

## Step 1: identity-api (service split)
- Created from core-api's account and token code: `AuthService`, `AccountService`, `TokenService`, `SigningKeyProvider`, `ProjectTokenService`, the auth, JWKS and internal project-token controllers, and the user, refresh-token and user-token entities. Behaviour and security rules are unchanged (see auth-design.md); the history before this step is in core-api/docs/changelog.md
- Changed: issuer `identity`; no role in the edge token; `User` has no role and no profile (names go to core-api); change password moved to `POST /api/account/change-password`; the refresh cookie path is `/api/identity/auth`
- New: own schema `identity` with a fresh `InitialCreate` migration; `CoreReadDbContext` to mint project tokens from core's tables; an outbox (`OutboxMessages`, `IOutbox`, `OutboxDispatcher`) that replaces inline email sending and tells core about new and confirmed users; `AdminSeeder` queues both events
- Uses platform-kit for results, ProblemDetails, request context, health and the internal key scheme (`InternalAuth:Callers:gateway`)
- Dockerfile with `api` and `migrate` targets, `IdentityApi.http`, `CLAUDE.md`, this `docs/` folder
