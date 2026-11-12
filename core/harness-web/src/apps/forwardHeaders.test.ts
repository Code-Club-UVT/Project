import { describe, expect, it } from "vitest";
import { responseHeaders, sanitizeHeaders } from "./forwardHeaders";

describe("sanitizeHeaders", () => {
  it("drops the headers an app must not set, whatever their case", () => {
    const result = sanitizeHeaders({
      Authorization: "Bearer stolen",
      Cookie: "a=b",
      Host: "evil.test",
      "Content-Length": "5",
      "X-User-Id": "1",
      "X-Internal-Key": "k",
      "Content-Type": "application/json",
      "X-Custom": "yes",
    });

    expect(result).toEqual({ "content-type": "application/json", "x-custom": "yes" });
  });

  it("returns an empty object for no headers", () => {
    expect(sanitizeHeaders(undefined)).toEqual({});
  });
});

describe("responseHeaders", () => {
  it("keeps string values and drops the others", () => {
    expect(responseHeaders({ "content-type": "text/plain", "set-cookie": ["a=b"], n: 1 })).toEqual({
      "content-type": "text/plain",
    });
  });

  it("never passes Set-Cookie to the app", () => {
    expect(responseHeaders({ "set-cookie": "a=b", etag: "x" })).toEqual({ etag: "x" });
  });
});
