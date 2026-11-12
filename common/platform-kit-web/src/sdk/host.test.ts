import { describe, expect, it, vi } from "vitest";
import { type FakeWindow, fakeBus, fakeWindow } from "./fakes";
import { createAppHost } from "./host";
import {
  type AppInit,
  type AppRequest,
  type AppResponse,
  PROTOCOL,
  type RequestMessage,
} from "./protocol";

const ORIGIN = "https://harness.test";
const INIT: AppInit = {
  user: { id: "u1", username: "ana" },
  role: "Admin",
  slug: "mock",
  locale: "en",
  theme: "dark",
};

function setup(
  overrides: { fetchApi?: (r: AppRequest, slug: string) => Promise<AppResponse> } = {},
) {
  const bus = fakeBus();
  const getInit = vi.fn(() => INIT);
  const fetchApi = vi.fn(
    overrides.fetchApi ??
      (async (r: AppRequest): Promise<AppResponse> => ({
        id: r.id,
        status: 200,
        headers: {},
        body: "ok",
      })),
  );
  const onNavigated = vi.fn();
  const host = createAppHost({ getInit, fetchApi, onNavigated, origin: ORIGIN, target: bus });
  const win = fakeWindow();
  host.register("mock", { contentWindow: win });
  return { bus, host, win, getInit, fetchApi, onNavigated };
}

const ready = { kit: PROTOCOL, type: "ready" };
const request = (id: string, path = "whoami"): RequestMessage => ({
  kit: PROTOCOL,
  type: "request",
  request: { id, method: "GET", path },
});
const lastMessage = (win: FakeWindow) =>
  win.posted[win.posted.length - 1]?.message as Record<string, unknown>;

describe("createAppHost", () => {
  it("ignores a message whose source is not a registered frame", () => {
    const { bus, win, getInit } = setup();

    bus.emit({ source: fakeWindow(), origin: ORIGIN, data: ready });

    expect(getInit).not.toHaveBeenCalled();
    expect(win.posted).toEqual([]);
  });

  it("ignores a message with a foreign origin", () => {
    const { bus, win, getInit } = setup();

    bus.emit({ source: win, origin: "https://evil.test", data: ready });

    expect(getInit).not.toHaveBeenCalled();
    expect(win.posted).toEqual([]);
  });

  it("ignores messages that are not of this protocol", () => {
    const { bus, win, getInit } = setup();

    bus.emit({ source: win, origin: ORIGIN, data: { type: "ready" } });
    bus.emit({ source: win, origin: ORIGIN, data: "ready" });
    bus.emit({ source: win, origin: ORIGIN, data: null });

    expect(getInit).not.toHaveBeenCalled();
  });

  it("replies init to ready, to the harness origin", () => {
    const { bus, win, getInit } = setup();

    bus.emit({ source: win, origin: ORIGIN, data: ready });

    expect(getInit).toHaveBeenCalledWith("mock");
    expect(win.posted).toEqual([
      { message: { kit: PROTOCOL, type: "init", init: INIT }, targetOrigin: ORIGIN },
    ]);
  });

  it("forwards a request to fetchApi and replies response with the same id", async () => {
    const { bus, win, fetchApi } = setup();

    bus.emit({ source: win, origin: ORIGIN, data: request("r1", "whoami") });
    await vi.waitFor(() => expect(win.posted).toHaveLength(1));

    expect(fetchApi).toHaveBeenCalledWith({ id: "r1", method: "GET", path: "whoami" }, "mock");
    expect(lastMessage(win)).toEqual({
      kit: PROTOCOL,
      type: "response",
      response: { id: "r1", status: 200, headers: {}, body: "ok" },
    });
  });

  it.each(["https://evil.test/x", "//evil.test/x", "../core/users", "a/%2e%2e/b", "a\\b"])(
    "rejects the unsafe path %s with 400 without calling fetchApi",
    async (path) => {
      const { bus, win, fetchApi } = setup();

      bus.emit({ source: win, origin: ORIGIN, data: request("r1", path) });
      await vi.waitFor(() => expect(win.posted).toHaveLength(1));

      expect(fetchApi).not.toHaveBeenCalled();
      const response = lastMessage(win).response as AppResponse;
      expect(response.id).toBe("r1");
      expect(response.status).toBe(400);
    },
  );

  it("answers 502 when fetchApi fails", async () => {
    const { bus, win } = setup({
      fetchApi: async () => {
        throw new Error("network down");
      },
    });

    bus.emit({ source: win, origin: ORIGIN, data: request("r1") });
    await vi.waitFor(() => expect(win.posted).toHaveLength(1));

    const response = lastMessage(win).response as AppResponse;
    expect(response.id).toBe("r1");
    expect(response.status).toBe(502);
  });

  it("reports the app's path through onNavigated", () => {
    const { bus, win, onNavigated } = setup();

    bus.emit({
      source: win,
      origin: ORIGIN,
      data: { kit: PROTOCOL, type: "navigated", path: "/items/3" },
    });

    expect(onNavigated).toHaveBeenCalledWith("mock", "/items/3");
  });

  it("broadcast reaches every registered frame", () => {
    const { host, win } = setup();
    const other = fakeWindow();
    host.register("other", { contentWindow: other });

    host.broadcast("theme", "light");

    const expected = {
      message: { kit: PROTOCOL, type: "event", name: "theme", payload: "light" },
      targetOrigin: ORIGIN,
    };
    expect(win.posted).toEqual([expected]);
    expect(other.posted).toEqual([expected]);
  });

  it("stops handling a frame after it is unregistered", () => {
    const bus = fakeBus();
    const host = createAppHost({
      getInit: () => INIT,
      fetchApi: vi.fn(),
      origin: ORIGIN,
      target: bus,
    });
    const win = fakeWindow();
    const unregister = host.register("mock", { contentWindow: win });

    unregister();
    bus.emit({ source: win, origin: ORIGIN, data: ready });
    host.broadcast("logout");

    expect(win.posted).toEqual([]);
  });

  it("dispose removes the listener and forgets the frames", () => {
    const { bus, host, win } = setup();

    host.dispose();

    expect(bus.listenerCount()).toBe(0);
    host.broadcast("logout");
    expect(win.posted).toEqual([]);
  });

  it("does not post to a frame without a window yet", () => {
    const bus = fakeBus();
    const host = createAppHost({
      getInit: () => INIT,
      fetchApi: vi.fn(),
      origin: ORIGIN,
      target: bus,
    });
    host.register("mock", { contentWindow: null });

    expect(() => host.broadcast("logout")).not.toThrow();
  });
});
