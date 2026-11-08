import { describe, expect, it } from "vitest";
import type { MyProject } from "../api/projects";
import { visibleApps } from "./visibleApps";

const project = (overrides: Partial<MyProject>): MyProject => ({
  projectId: crypto.randomUUID(),
  slug: "foo",
  name: "Foo",
  description: null,
  roleId: crypto.randomUUID(),
  roleName: "User",
  hasFrontend: true,
  hasBackend: true,
  ...overrides,
});

describe("visibleApps", () => {
  it("keeps only projects with a frontend", () => {
    const apps = visibleApps([
      project({ slug: "web", hasFrontend: true, hasBackend: false }),
      project({ slug: "api-only", hasFrontend: false, hasBackend: true }),
      project({ slug: "nothing", hasFrontend: false, hasBackend: false }),
    ]);

    expect(apps.map((a) => a.slug)).toEqual(["web"]);
  });

  it("hides core and identity even if they had a frontend", () => {
    const apps = visibleApps([
      project({ slug: "core", hasFrontend: true }),
      project({ slug: "identity", hasFrontend: true }),
      project({ slug: "mock", hasFrontend: true }),
    ]);

    expect(apps.map((a) => a.slug)).toEqual(["mock"]);
  });

  it("sorts by name, ignoring case", () => {
    const apps = visibleApps([
      project({ slug: "b", name: "beta" }),
      project({ slug: "a", name: "Alpha" }),
      project({ slug: "c", name: "Gamma" }),
    ]);

    expect(apps.map((a) => a.slug)).toEqual(["a", "b", "c"]);
  });

  it("does not change the list it was given", () => {
    const input = [project({ slug: "b", name: "B" }), project({ slug: "a", name: "A" })];

    visibleApps(input);

    expect(input.map((p) => p.slug)).toEqual(["b", "a"]);
  });

  it("returns an empty list for no projects", () => {
    expect(visibleApps([])).toEqual([]);
  });
});
