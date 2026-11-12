# Implementation

## What it is

A tiny app that exercises the harness's iframe integration. Slug `mock`, served by the gateway at `/apps/mock/` (publicly `/<env>/apps/mock/`).

## Screens (hash routes)

- `#/`: shows the user's name and role (from `init`; the role is a hint, the API decides), and two buttons: `GET whoami` and `GET whoami/admin`. They call `/api/mock/...` through the harness with `client.api.fetch` and print the status and body (`whoami/admin` is 403 for non-admins).
- `#/about`: a static page, to check deep links: the app reports its path with `reportPath`, so the harness address is `/app/mock/about` (below the environment's base path), and a reload opens the tab at that page.

## Icon

`public/icon.svg` is copied to the root of the build, so the gateway serves it at `/apps/mock/icon.svg`. The harness's app menu uses that file for every app (the convention is `icon.svg` at the app's root) and shows a default icon when it is missing.

It also follows the harness: `theme` (sets `data-theme` on `<html>`), `role-changed` (updates the role line) and `logout`.

## Build and run

- Vite with `base: "./"`: relative assets and hash routes, so one build works under `/dev/apps/mock/` and `/apps/mock/`.
- The image is nginx (unprivileged, port 8080, read-only root filesystem) serving `dist`, with `/healthz`. The gateway removes the `/apps/mock` prefix, so the server sees `/` and `/assets/...`.
- `npm run ci` (Biome and type check) must pass for the image to build.

## Registering it (once per environment, as a core admin)

Set the `mock` project's `frontendUrl` to `http://mock-web:8080/` (`PUT /api/core/projects/{id}`, keeping its other fields) and make sure the user is a member. The gateway picks it up within its poll interval (30 s); the harness then shows a `Mock` tab. Compose: service `mock-web` in `stack-v2/docker-compose.yml` on the server (profile `mock`) and in `local-deploy/docker-compose.yml`.
