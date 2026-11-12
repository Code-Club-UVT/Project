import { describe, expect, it } from "vitest";
import { closeTab, openTab, restoreTabs, setTabPath, toSavedTabs } from "./tabList";

describe("openTab / closeTab", () => {
  it("appends a new tab at the start page", () => {
    expect(openTab([{ projectId: "a", path: "/x" }], "b")).toEqual([
      { projectId: "a", path: "/x" },
      { projectId: "b", path: "" },
    ]);
  });

  it("does not open the same app twice and keeps its path", () => {
    expect(openTab([{ projectId: "a", path: "/x" }], "a")).toEqual([
      { projectId: "a", path: "/x" },
    ]);
  });

  it("closes only the given tab and keeps the order", () => {
    const tabs = [
      { projectId: "a", path: "" },
      { projectId: "b", path: "" },
      { projectId: "c", path: "" },
    ];
    expect(closeTab(tabs, "b").map((tab) => tab.projectId)).toEqual(["a", "c"]);
    expect(closeTab(tabs, "zzz")).toEqual(tabs);
  });
});

describe("setTabPath", () => {
  it("returns the same array when the path did not change or the tab is not open", () => {
    const tabs = [{ projectId: "a", path: "/x" }];
    expect(setTabPath(tabs, "a", "/x")).toBe(tabs);
    expect(setTabPath(tabs, "b", "/y")).toBe(tabs);
  });

  it("updates the path of one tab", () => {
    const tabs = [
      { projectId: "a", path: "" },
      { projectId: "b", path: "" },
    ];
    expect(setTabPath(tabs, "b", "/y")).toEqual([
      { projectId: "a", path: "" },
      { projectId: "b", path: "/y" },
    ]);
  });
});

describe("restoreTabs", () => {
  const openable = new Set(["a", "b"]);

  it("keeps the saved order and turns '/' into the start page", () => {
    expect(
      restoreTabs(
        [
          { projectId: "b", lastPath: "/items/3" },
          { projectId: "a", lastPath: "/" },
        ],
        openable,
      ),
    ).toEqual([
      { projectId: "b", path: "/items/3" },
      { projectId: "a", path: "" },
    ]);
  });

  it("drops apps the user can no longer open and repeated ones", () => {
    expect(
      restoreTabs(
        [
          { projectId: "gone", lastPath: "/" },
          { projectId: "a", lastPath: "/one" },
          { projectId: "a", lastPath: "/two" },
        ],
        openable,
      ),
    ).toEqual([{ projectId: "a", path: "/one" }]);
  });
});

describe("toSavedTabs", () => {
  it("saves the start page as '/'", () => {
    expect(
      toSavedTabs([
        { projectId: "a", path: "" },
        { projectId: "b", path: "/x" },
      ]),
    ).toEqual([
      { projectId: "a", lastPath: "/" },
      { projectId: "b", lastPath: "/x" },
    ]);
  });
});
