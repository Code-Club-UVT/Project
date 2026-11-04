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
