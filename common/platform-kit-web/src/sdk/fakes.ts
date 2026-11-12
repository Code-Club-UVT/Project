import type { IncomingMessage, MessageListenerTarget, PostTarget } from "./protocol";

/** Test helpers: stand-ins for windows, so the bridge can be tested without a DOM. */
export interface FakeWindow extends PostTarget {
  posted: { message: unknown; targetOrigin: string }[];
}

export function fakeWindow(): FakeWindow {
  const posted: FakeWindow["posted"] = [];
  return {
    posted,
    postMessage(message, targetOrigin) {
      posted.push({ message, targetOrigin });
    },
  };
}

export interface FakeBus extends MessageListenerTarget {
  emit(event: IncomingMessage): void;
  listenerCount(): number;
}

export function fakeBus(): FakeBus {
  const listeners = new Set<(event: IncomingMessage) => void>();
  return {
    addEventListener(_type, listener) {
      listeners.add(listener);
    },
    removeEventListener(_type, listener) {
      listeners.delete(listener);
    },
    emit(event) {
      for (const listener of [...listeners]) listener(event);
    },
    listenerCount: () => listeners.size,
  };
}
