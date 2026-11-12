import { describe, expect, it } from "vitest";
import { appIconUrl, appPathFromRoute, appRoute, appUrl, routerBasename } from "./appUrl";

describe("appIconUrl", () => {
  it("is icon.svg at the root of the app", () => {
    expect(appIconUrl("/dev/", "mock")).toBe("/dev/apps/mock/icon.svg");
    expect(appIconUrl("/", "mock")).toBe("/apps/mock/icon.svg");
  });
});

describe("appRoute", () => {
  it("has no hash and no doubled slashes", () => {
    expect(appRoute("mock")).toBe("/app/mock");
    expect(appRoute("mock", "")).toBe("/app/mock");
    expect(appRoute("mock", "/about")).toBe("/app/mock/about");
    expect(appRoute("mock", "items/3")).toBe("/app/mock/items/3");
  });
});

describe("routerBasename", () => {
  it("drops the trailing slash and has none for the root", () => {
    expect(routerBasename("/dev/")).toBe("/dev");
    expect(routerBasename("/a/b/")).toBe("/a/b");
    expect(routerBasename("/")).toBeUndefined();
  });
});

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
