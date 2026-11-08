import { describe, expect, it } from "vitest";
import { appPathFromRoute, appUrl } from "./appUrl";

describe("appUrl", () => {
  it.each([
    ["/dev/", "mock", undefined, "/dev/apps/mock/"],
    ["/", "mock", undefined, "/apps/mock/"],
    ["/preprod/", "my-app", "", "/preprod/apps/my-app/"],
    ["/dev/", "mock", "/", "/dev/apps/mock/"],
  ])("base %s, slug %s, path %s gives %s", (base, slug, path, expected) => {
    expect(appUrl(base, slug, path)).toBe(expected);
  });

  it("appends a path without doubling slashes", () => {
    expect(appUrl("/dev/", "mock", "/items/3")).toBe("/dev/apps/mock/#/items/3");
    expect(appUrl("/", "mock", "items/3")).toBe("/apps/mock/#/items/3");
  });

  it("accepts a base path without a trailing slash", () => {
    expect(appUrl("/dev", "mock")).toBe("/dev/apps/mock/");
  });
});

describe("appPathFromRoute", () => {
  it("returns an empty path for the app's root", () => {
    expect(appPathFromRoute("")).toBe("");
    expect(appPathFromRoute(undefined)).toBe("");
    expect(appPathFromRoute("/")).toBe("");
  });

  it("returns the path with one leading slash", () => {
    expect(appPathFromRoute("items/3")).toBe("/items/3");
    expect(appPathFromRoute("/items/3")).toBe("/items/3");
  });
});
