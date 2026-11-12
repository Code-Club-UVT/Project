import {
  type AppEventName,
  type AppInit,
  type AppResponse,
  type IncomingMessage,
  isHostMessage,
  isSafeApiPath,
  type MessageListenerTarget,
  type PostTarget,
  PROTOCOL,
} from "./protocol";

export interface AppClientOptions {
  /** Defaults to `window.parent`. */
  parent?: PostTarget;
  /** Where messages arrive. Defaults to `window`. */
  self?: MessageListenerTarget;
  /** The harness origin (the same origin as the app). Defaults to `window.location.origin`. */
  origin?: string;
}

export interface AppClient {
  /** What the harness told the app: user, role (a UI hint only), slug, locale, theme. */
  readonly init: AppInit;
  api: {
    /** Calls `/api/<slug>/<path>` as the signed-in user, through the harness. Same shape as `fetch`; string bodies only. */
    fetch(path: string, init?: RequestInit): Promise<Response>;
  };
  /** Subscribes to a harness event; returns the unsubscribe function. */
  on(name: AppEventName, handler: (payload?: unknown) => void): () => void;
  /** Tells the harness the app's current path, so it can put it in the address (deep links). */
  reportPath(path: string): void;
}

const READY_RETRY_MS = 250;
const READY_MAX_TRIES = 40;
const BODYLESS_STATUS = new Set([101, 204, 205, 304]);

let sequence = 0;
const nextId = () => `${++sequence}-${Math.random().toString(36).slice(2, 10)}`;

function toResponse(response: AppResponse): Response {
  // `Response` only accepts 200-599; anything else means the harness could not make the call.
  const status = response.status >= 200 && response.status <= 599 ? response.status : 502;
  return new Response(BODYLESS_STATUS.has(status) ? null : response.body, {
    status,
    headers: response.headers,
  });
}

/**
 * App side of the bridge. Resolves once the harness answered with `init`. The harness may register the frame a moment
 * after it loads, so `ready` is sent again until it answers.
 */
export function createAppClient(options: AppClientOptions = {}): Promise<AppClient> {
  const parent = options.parent ?? (window.parent as PostTarget);
  const self = options.self ?? (window as unknown as MessageListenerTarget);
  const origin = options.origin ?? window.location.origin;

  const pending = new Map<string, (response: AppResponse) => void>();
  const handlers = new Map<AppEventName, Set<(payload?: unknown) => void>>();
  let onInit: ((init: AppInit) => void) | undefined;

  const send = (message: unknown) => parent.postMessage(message, origin);

  self.addEventListener("message", (event: IncomingMessage) => {
    if (event.source !== parent || event.origin !== origin || !isHostMessage(event.data)) return;

    const message = event.data;
    switch (message.type) {
      case "init":
        onInit?.(message.init);
        onInit = undefined;
        break;
      case "response":
        pending.get(message.response.id)?.(message.response);
        pending.delete(message.response.id);
        break;
      case "event":
        for (const handler of handlers.get(message.name) ?? []) handler(message.payload);
        break;
    }
  });

  return new Promise<AppClient>((resolve, reject) => {
    let tries = 0;
    const timer = setInterval(() => {
      if (++tries >= READY_MAX_TRIES) {
        clearInterval(timer);
        reject(new Error("The harness did not answer. Is the app running inside the harness?"));
        return;
      }
      send({ kit: PROTOCOL, type: "ready" });
    }, READY_RETRY_MS);

    onInit = (init) => {
      clearInterval(timer);
      resolve({
        init,
        api: {
          async fetch(path, requestInit) {
            if (!isSafeApiPath(path)) throw new TypeError(`Not a valid API path: ${path}`);
            const body = requestInit?.body;
            if (body !== undefined && body !== null && typeof body !== "string") {
              throw new TypeError("api.fetch only supports string bodies (use JSON.stringify).");
            }

            const headers: Record<string, string> = {};
            new Headers(requestInit?.headers).forEach((value, key) => {
              headers[key] = value;
            });

            const id = nextId();
            const answer = new Promise<AppResponse>((done) => pending.set(id, done));
            send({
              kit: PROTOCOL,
              type: "request",
              request: {
                id,
                method: requestInit?.method ?? "GET",
                path,
                headers,
                ...(typeof body === "string" ? { body } : {}),
              },
            });

            return toResponse(await answer);
          },
        },
        on(name, handler) {
          const set = handlers.get(name) ?? new Set();
          set.add(handler);
          handlers.set(name, set);
          return () => {
            set.delete(handler);
          };
        },
        reportPath(path) {
          send({ kit: PROTOCOL, type: "navigated", path });
        },
      });
    };

    send({ kit: PROTOCOL, type: "ready" });
  });
}
