# Changelog

## Step 5: tests
- New `PlatformKit.Tests` (85 tests): `Result`, `Error`, `PagedResult`; `ResultControllerBase` status mapping; the internal-key handler (right, wrong, near-miss and doubled keys) and its options checks; `InternalKeyHandler` and `CorrelationIdHandler`; the request context and the correlation id rules; the exception handler; `JwksKeyStore` (first download, refresh time, unknown key, backoff, failures and empty key sets keep the old keys); and the token validation of every API against a real local JWKS endpoint (wrong audience, issuer, expiry, key, algorithm, unsigned and altered tokens)
- `PlatformKit.csproj` excludes the folder and has `InternalsVisibleTo` for the tests

## Step 1: library
- Created the `GatewayAuth` class library with `AddGatewayAuthentication` (ES256, issuer, audience = slug, keys from the JWKS), `JwksKeyStore`, `IRequestContext` with `AddRequestContext` / `UseRequestContext`, the correlation id middleware and `CorrelationIdHandler`, `AddGatewayHealth` / `MapGatewayHealth`
- Added `CLAUDE.md`, this `docs/` folder and a `.gitignore`
- The first version used the stock `ConfigurationManager` for the keys; testing showed the first request after a key rotation failed, so it was replaced by `JwksKeyStore`
- Verified with a throwaway sample API against the real core-api and PostgreSQL (26 checks, all passed; the sample was deleted). See [implementation.md](implementation.md)

## Step 2: fixes from the code review
- `JwksKeyStore`: a failed download is retried after 5 seconds instead of after the full unknown-key interval; the timestamps are read and written atomically; a request does not queue behind a running download unless no key is loaded yet
- Verified with the real core-api and a throwaway API: an API started while core-api was down rejected the first token, and accepted the same token seconds after core-api came back (it used to wait a minute)

## Step 4: TooManyRequests
- Public surface: `ErrorType.TooManyRequests`, `Error.TooManyRequests(code, message)`, and `ResultControllerBase` answers it with 429 (ProblemDetails with the `code`). Added for identity's login and email throttling; no other service is affected (a `switch` over `ErrorType` elsewhere would need the new case)

## Step 3: platform-kit (service split)
- Renamed the folder, project and namespaces from `gateway-auth` / `GatewayAuth` to `platform-kit` / `PlatformKit`; the configuration section is now `PlatformAuth`; `AddGatewayAuthentication`, `AddGatewayHealth`, `MapGatewayHealth` became `AddPlatformAuthentication`, `AddPlatformHealth`, `MapPlatformHealth`. Breaking for every service; all were updated in the same change
- Moved in from core-api: `Result`, `Result<T>`, `Error`, `ErrorType`, `PagedResult<T>` (`PlatformKit.Results`), `ResultControllerBase` and the global exception handler (`PlatformKit.Web`, `AddPlatformProblemDetails`)
- New: the `InternalKey` scheme with one key per caller (`AddInternalKeyAuthentication`, `InternalAuth:Callers`) and `AddInternalClient` for calling another service's `/internal` endpoints
- Tokens are now issued by identity-api (issuer `identity`); the validation itself did not change
- Verified on the dev server through all five services: project tokens (audience per project), the internal key scheme (gateway to identity, identity to core and email), the shared ProblemDetails format (`code`), the correlation id
