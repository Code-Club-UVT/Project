import { afterEach, describe, expect, it, vi } from "vitest";
import { createAppClient } from "./client";
import { fakeBus, fakeWindow } from "./fakes";
import { type AppInit, PROTOCOL, type RequestMessage } from "./protocol";

const ORIGIN = "https://harness.test";
const INIT: AppInit = {
  user: { id: "u1", username: "ana" },
  role: "User",
  slug: "mock",
  locale: "ro",
  theme: "light",
};

afterEach(() => {
  vi.useRealTimers();
});

async function connected() {
  const parent = fakeWindow();
  const self = fakeBus();
  const pending = createAppClient({ parent, self, origin: ORIGIN });
  self.emit({ source: parent, origin: ORIGIN, data: { kit: PROTOCOL, type: "init", init: INIT } });
  const client = await pending;
  return { parent, self, client };
}

const sentRequests = (parent: ReturnType<typeof fakeWindow>) =>
  parent.posted
    .map((p) => p.message as { type: string })
    .filter((m): m is RequestMessage => m.type === "request");

describe("createAppClient", () => {
  it("posts ready to the parent and resolves after init", async () => {
    const { parent, client } = await connected();

    expect(parent.posted[0]).toEqual({
      message: { kit: PROTOCOL, type: "ready" },
      targetOrigin: ORIGIN,
    });
    expect(client.init).toEqual(INIT);
  });

  it("ignores an init that is not from the parent or has the wrong origin", async () => {
    const parent = fakeWindow();
    const self = fakeBus();
    let resolved = false;
    createAppClient({ parent, self, origin: ORIGIN }).then(() => {
      resolved = true;
    });

    self.emit({
      source: fakeWindow(),
      origin: ORIGIN,
      data: { kit: PROTOCOL, type: "init", init: INIT },
    });
    self.emit({
      source: parent,
      origin: "https://evil.test",
      data: { kit: PROTOCOL, type: "init", init: INIT },
    });
    await Promise.resolve();

    expect(resolved).toBe(false);
  });

  it("sends ready again until the harness answers", async () => {
    vi.useFakeTimers();
    const parent = fakeWindow();
    const self = fakeBus();
    createAppClient({ parent, self, origin: ORIGIN });

    await vi.advanceTimersByTimeAsync(600);

    expect(parent.posted.length).toBeGreaterThan(1);
  });

  it("api.fetch posts a request and resolves with the matching Response", async () => {
    const { parent, self, client } = await connected();

    const call = client.api.fetch("whoami", {
      method: "POST",
      headers: { "X-A": "1" },
      body: '{"a":1}',
    });
    const [sent] = sentRequests(parent);
    expect(sent.request).toMatchObject({ method: "POST", path: "whoami", body: '{"a":1}' });
    expect(sent.request.headers).toMatchObject({ "x-a": "1" });
    self.emit({
      source: parent,
      origin: ORIGIN,
      data: {
        kit: PROTOCOL,
        type: "response",
        response: {
          id: sent.request.id,
          status: 201,
          headers: { "content-type": "application/json" },
          body: '{"ok":true}',
        },
      },
    });

    const response = await call;
    expect(response.status).toBe(201);
    expect(await response.json()).toEqual({ ok: true });
  });

  it("uses unique request ids and matches out-of-order responses by id", async () => {
    const { parent, self, client } = await connected();

    const first = client.api.fetch("a");
    const second = client.api.fetch("b");
    const [one, two] = sentRequests(parent);
    expect(one.request.id).not.toBe(two.request.id);
    const respond = (id: string, body: string) =>
      self.emit({
        source: parent,
        origin: ORIGIN,
        data: { kit: PROTOCOL, type: "response", response: { id, status: 200, headers: {}, body } },
      });
    respond(two.request.id, "B");
    respond(one.request.id, "A");

    expect(await (await first).text()).toBe("A");
    expect(await (await second).text()).toBe("B");
  });

  it("builds a body-less Response for 204", async () => {
    const { parent, self, client } = await connected();

    const call = client.api.fetch("x", { method: "DELETE" });
    const [sent] = sentRequests(parent);
    self.emit({
      source: parent,
      origin: ORIGIN,
      data: {
        kit: PROTOCOL,
        type: "response",
        response: { id: sent.request.id, status: 204, headers: {}, body: "" },
      },
    });

    expect((await call).status).toBe(204);
  });

  it("rejects an unsafe path and a non-string body without posting", async () => {
    const { parent, client } = await connected();

    await expect(client.api.fetch("https://evil.test/x")).rejects.toThrow(TypeError);
    await expect(client.api.fetch("a", { method: "POST", body: new FormData() })).rejects.toThrow(
      TypeError,
    );

    expect(sentRequests(parent)).toHaveLength(0);
  });

  it("on() delivers events and returns an unsubscribe", async () => {
    const { parent, self, client } = await connected();
    const handler = vi.fn();
    const off = client.on("theme", handler);

    self.emit({
      source: parent,
      origin: ORIGIN,
      data: { kit: PROTOCOL, type: "event", name: "theme", payload: "dark" },
    });
    off();
    self.emit({
      source: parent,
      origin: ORIGIN,
      data: { kit: PROTOCOL, type: "event", name: "theme", payload: "light" },
    });

    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith("dark");
  });

  it("reportPath posts navigated", async () => {
    const { parent, client } = await connected();

    client.reportPath("/items/3");

    expect(parent.posted.at(-1)).toEqual({
      message: { kit: PROTOCOL, type: "navigated", path: "/items/3" },
      targetOrigin: ORIGIN,
    });
  });
});
