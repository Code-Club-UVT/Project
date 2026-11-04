# Backlog and open points

## Next steps
- The gateway already uses it for the edge token (audience `gateway`) and for the request context / health. Real project APIs will use it for their project tokens.
- Dockerfiles of APIs that reference this library must build with the repo root as context.

## Open decisions
- **Permissions**: roles only for now. If permissions come back, add a `permission` claim and a policy helper here.
- **Packaging**: publish as a NuGet package (private feed or GitHub Packages) if APIs leave this repo.
- **Startup prefetch of the keys**: the first token triggers the first download. A hosted service could download at startup so the first request is not slower, and so a wrong `JwksUrl` shows in the log immediately.
- **Async key download**: the validator callback is synchronous, so a download blocks one request. Fine while rare; revisit if the core API is often slow.
- **Using it in core-api**: core-api keeps its own validation (it holds the private key). It could still use `IRequestContext` and the correlation id middleware.
- **Real client IP**: document or wrap a `UseForwardedHeaders` setup trusting the gateway.
- **Tests**: no test project. The behaviours listed in `implementation.md` were checked by hand with a throwaway API; an automated test project would keep them checked.

## TODO: publish each API's OpenAPI schema (for the planned APIM-like app)

- Goal: every project API serves its OpenAPI JSON (the schema only, no Swagger UI) anonymously, and the gateway makes it public at `/api/<slug>/openapi/v1.json` in every environment. The APIM app reads the list of APIs (slug, name) from core-api's project list and the schema from that URL.
- Suggested shape: a helper here, `MapGatewayOpenApi()`, that maps `/api/openapi/v1.json` with `AllowAnonymous()`, plus a document transformer so every API documents itself the same way (see core-api/docs/backlog.md for the path convention). This needs the `Microsoft.AspNetCore.OpenApi` package in this library: ask before adding it.
- Open decision: public in prod too (reveals the API surface) or only behind a token/IP rule for the APIM app. Decide before building.
