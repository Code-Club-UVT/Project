# Changelog

## Repository restructure
- Moved to `common/platform-kit-web/` (`core/` for the platform, `common/` for the shared libraries, one top-level folder per app). Paths in the project files, Dockerfile and docs follow; no behaviour change.

## Step 1: the bridge
- New package `platform-kit-web` with the `sdk` entry point: protocol, `createAppHost` (harness) and `createAppClient` (app)
- 25 vitest tests with fake windows (origin and source checks, unsafe paths, request/response matching, events, retries of `ready`)
