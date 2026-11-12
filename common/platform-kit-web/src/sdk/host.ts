import {
  type AppEventName,
  type AppInit,
  type AppRequest,
  type AppResponse,
  type HostMessage,
  type IncomingMessage,
  isAppMessage,
  isSafeApiPath,
  type MessageListenerTarget,
  type PostTarget,
  PROTOCOL,
} from "./protocol";

/** The part of an `HTMLIFrameElement` the host needs. */
export interface FrameLike {
  contentWindow: PostTarget | null;
}

export interface AppHostOptions {
  /** What to tell the app of this project when it says `ready`. */
  getInit(slug: string): AppInit;
  /** Makes the call to `/api/<slug>/<path>` as the signed-in user (the harness adds the token). */
  fetchApi(request: AppRequest, slug: string): Promise<AppResponse>;
  /** The app moved to another path inside itself (for deep links). */
  onNavigated?(slug: string, path: string): void;
  /** The harness origin; messages from any other origin are ignored. Defaults to `window.location.origin`. */
  origin?: string;
  /** Where messages arrive. Defaults to `window`. */
  target?: MessageListenerTarget;
}

export interface AppHost {
  /** Starts serving this iframe for the project; returns a function that stops. Register before the frame loads. */
  register(slug: string, frame: FrameLike): () => void;
  /** Sends an event to every registered app. */
  broadcast(name: AppEventName, payload?: unknown): void;
  dispose(): void;
}

function failure(id: string, status: number, message: string): AppResponse {
  return { id, status, headers: { "content-type": "text/plain" }, body: message };
}

/**
 * Harness side of the bridge. It only listens to windows that were registered as frames of apps, and only from its
 * own origin; the token never leaves the harness: apps ask it to make the API calls.
 */
export function createAppHost(options: AppHostOptions): AppHost {
  const origin = options.origin ?? window.location.origin;
  const target = options.target ?? (window as unknown as MessageListenerTarget);
  const frames = new Map<FrameLike, string>();

  const post = (frame: FrameLike, message: HostMessage) => {
    frame.contentWindow?.postMessage(message, origin);
  };

  const reply = (frame: FrameLike, response: AppResponse) => {
    post(frame, { kit: PROTOCOL, type: "response", response });
  };

  const forward = async (frame: FrameLike, slug: string, request: AppRequest) => {
    if (!isSafeApiPath(request.path)) {
      reply(frame, failure(request.id, 400, "The path is not allowed."));
      return;
    }

    try {
      const response = await options.fetchApi(request, slug);
      reply(frame, { ...response, id: request.id });
    } catch {
      reply(frame, failure(request.id, 502, "The call could not be made."));
    }
  };

  const onMessage = (event: IncomingMessage) => {
    if (event.origin !== origin) return;

    let frame: FrameLike | undefined;
    let slug: string | undefined;
    for (const [candidate, candidateSlug] of frames) {
      if (candidate.contentWindow !== null && candidate.contentWindow === event.source) {
        frame = candidate;
        slug = candidateSlug;
        break;
      }
    }

    if (frame === undefined || slug === undefined || !isAppMessage(event.data)) return;

    const message = event.data;
    switch (message.type) {
      case "ready":
        post(frame, { kit: PROTOCOL, type: "init", init: options.getInit(slug) });
        break;
      case "navigated":
        options.onNavigated?.(slug, message.path);
        break;
      case "request":
        void forward(frame, slug, message.request);
        break;
    }
  };

  target.addEventListener("message", onMessage);

  return {
    register(slug, frame) {
      frames.set(frame, slug);
      return () => {
        frames.delete(frame);
      };
    },
    broadcast(name, payload) {
      for (const frame of frames.keys()) {
        post(
          frame,
          payload === undefined
            ? { kit: PROTOCOL, type: "event", name }
            : { kit: PROTOCOL, type: "event", name, payload },
        );
      }
    },
    dispose() {
      target.removeEventListener("message", onMessage);
      frames.clear();
    },
  };
}
