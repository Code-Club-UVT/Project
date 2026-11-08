# Implementation

## What it is

`platform-kit-web/sdk`: the messages between the harness and an app in an iframe at `<harness base path>apps/<slug>/`. Both sides are on the same origin. The harness owns the user's session; the app never sees the token.

## Protocol (`src/sdk/protocol.ts`)

Every message is `{ kit: "platform-kit-web/1", type, ... }`; other messages are ignored.

| Direction | `type` | Content |
|---|---|---|
| app → harness | `ready` | the app loaded; sent again every 250 ms until the harness answers (up to 40 tries) |
| harness → app | `init` | `AppInit`: `user {id, username}`, `role` (a UI hint only), `slug`, `locale`, `theme` |
| app → harness | `request` | `AppRequest {id, method, path, headers?, body?}`; `path` is relative to `/api/<slug>/` |
| harness → app | `response` | `AppResponse {id, status, headers, body}`, matched by `id` |
| app → harness | `navigated` | `path`: the app's current path (the harness puts it in the address for deep links) |
| harness → app | `event` | `name` (`role-changed`, `theme`, `locale`, `logout`) and an optional `payload` |

`path` must stay below `/api/<slug>/`: no scheme, no `//`, no backslash or control characters, no `..` segment (also percent-encoded). Both sides check this; the host answers 400.

## Harness side: `createAppHost(options)`

- `options`: `getInit(slug)`, `fetchApi(request, slug)` (the harness makes the call with its token and the shared refresh), optional `onNavigated(slug, path)`, `origin` and `target` (default: `window`).
- `register(slug, frame)` returns an unregister function; register before the iframe's `src` is set.
- A message is accepted only if its origin equals the harness origin and `event.source` is the `contentWindow` of a registered frame.
- A failing `fetchApi` becomes a 502 response; an unsafe path a 400 response.
- `broadcast(name, payload?)` sends an event to every registered app; `dispose()` removes the listener.

## App side: `createAppClient(options?)`

- Resolves with `{ init, api, on, reportPath }` once the harness answered. Rejects after about 10 s without an answer.
- `api.fetch(path, init?)` has the shape of `fetch`; only string bodies (use `JSON.stringify`). The `Response` is rebuilt from the harness's answer (204 and 304 have no body; statuses outside 200-599 become 502).
- Messages are accepted only from `window.parent` and the same origin.

## Testing

`npm test`: the bridge is tested with fake windows (`src/sdk/fakes.ts`), no DOM. The behaviour in a real browser (iframe, origin) is checked end to end on the dev environment.

## Using it

In an app: `import { createAppClient } from "platform-kit-web/sdk"`, build with a relative base (`base: "./"` in Vite) and hash routing, because the harness serves the app under a path that differs per environment (`/dev/apps/<slug>/`, `/apps/<slug>/`).
