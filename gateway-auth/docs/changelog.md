# Changelog

## Step 1: library
- Created the `GatewayAuth` class library with `AddGatewayAuthentication` (ES256, issuer, audience = slug, keys from the JWKS), `JwksKeyStore`, `IRequestContext` with `AddRequestContext` / `UseRequestContext`, the correlation id middleware and `CorrelationIdHandler`, `AddGatewayHealth` / `MapGatewayHealth`
- Added `CLAUDE.md`, this `docs/` folder and a `.gitignore`
- The first version used the stock `ConfigurationManager` for the keys; testing showed the first request after a key rotation failed, so it was replaced by `JwksKeyStore`
- Verified with a throwaway sample API against the real core-api and PostgreSQL (26 checks, all passed; the sample was deleted). See [implementation.md](implementation.md)

## Step 2: fixes from the code review
- `JwksKeyStore`: a failed download is retried after 5 seconds instead of after the full unknown-key interval; the timestamps are read and written atomically; a request does not queue behind a running download unless no key is loaded yet
- Verified with the real core-api and a throwaway API: an API started while core-api was down rejected the first token, and accepted the same token seconds after core-api came back (it used to wait a minute)
