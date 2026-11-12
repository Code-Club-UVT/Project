/**
 * The messages between the harness and an app running in an iframe of the harness (same origin, first-party code).
 * Every message is an object with `kit: PROTOCOL` and a `type`; anything else is ignored.
 */
export const PROTOCOL = "platform-kit-web/1";

export type AppEventName = "role-changed" | "theme" | "locale" | "logout";

export interface AppUser {
  id: string;
  username: string;
}

/** What the harness tells an app when it is ready. The role is a UI hint only: the backend enforces access. */
export interface AppInit {
  user: AppUser;
  role: string | null;
  slug: string;
  locale: string;
  theme: "light" | "dark";
}

/** A call to the project's API. `path` is relative to `/api/<slug>/`. */
export interface AppRequest {
  id: string;
  method: string;
  path: string;
  headers?: Record<string, string>;
  body?: string;
}

export interface AppResponse {
  id: string;
  status: number;
  headers: Record<string, string>;
  body: string;
}

interface Envelope {
  kit: typeof PROTOCOL;
}

export type ReadyMessage = Envelope & { type: "ready" };
export type RequestMessage = Envelope & { type: "request"; request: AppRequest };
export type NavigatedMessage = Envelope & { type: "navigated"; path: string };
export type InitMessage = Envelope & { type: "init"; init: AppInit };
export type ResponseMessage = Envelope & { type: "response"; response: AppResponse };
export type EventMessage = Envelope & { type: "event"; name: AppEventName; payload?: unknown };

/** App to harness. */
export type AppMessage = ReadyMessage | RequestMessage | NavigatedMessage;
/** Harness to app. */
export type HostMessage = InitMessage | ResponseMessage | EventMessage;

/** The part of a `MessageEvent` the bridge looks at. */
export interface IncomingMessage {
  source: unknown;
  origin: string;
  data: unknown;
}

/** Something messages can be received on (a `Window`). */
export interface MessageListenerTarget {
  addEventListener(type: "message", listener: (event: IncomingMessage) => void): void;
  removeEventListener(type: "message", listener: (event: IncomingMessage) => void): void;
}

/** Something messages can be sent to (a `Window`). */
export interface PostTarget {
  postMessage(message: unknown, targetOrigin: string): void;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

/** True for objects of this protocol with a string `type`. */
export function isKitMessage(data: unknown): data is { kit: typeof PROTOCOL; type: string } {
  return isRecord(data) && data.kit === PROTOCOL && typeof data.type === "string";
}

export function isAppMessage(data: unknown): data is AppMessage {
  if (!isKitMessage(data)) return false;
  const message = data as Record<string, unknown>;
  switch (data.type) {
    case "ready":
      return true;
    case "navigated":
      return typeof message.path === "string";
    case "request": {
      const request = message.request;
      return (
        isRecord(request) &&
        typeof request.id === "string" &&
        typeof request.method === "string" &&
        typeof request.path === "string" &&
        (request.body === undefined || typeof request.body === "string")
      );
    }
    default:
      return false;
  }
}

export function isHostMessage(data: unknown): data is HostMessage {
  if (!isKitMessage(data)) return false;
  const message = data as Record<string, unknown>;
  switch (data.type) {
    case "init":
      return isRecord(message.init);
    case "response":
      return isRecord(message.response) && typeof message.response.id === "string";
    case "event":
      return typeof message.name === "string";
    default:
      return false;
  }
}

function hasControlCharacter(value: string): boolean {
  for (const char of value) {
    const code = char.charCodeAt(0);
    if (code <= 0x1f || char === "\\") return true;
  }
  return false;
}

/**
 * A path for `api.fetch` must stay below `/api/<slug>/`: no scheme, no `//`, no backslash or control characters and no `..`
 * segment (also when percent-encoded).
 */
export function isSafeApiPath(path: string): boolean {
  if (path.startsWith("//") || /^[a-z][a-z0-9+.-]*:/i.test(path)) return false;
  if (hasControlCharacter(path)) return false;

  let decoded: string;
  try {
    decoded = decodeURIComponent(path);
  } catch {
    return false;
  }

  return !decoded.split("/").includes("..") && !path.split("/").includes("..");
}
