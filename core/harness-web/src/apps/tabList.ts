import type { SavedTab } from "../api/tabs";

/**
 * An app the user has open. `path` is the place inside the app, as the bridge reports it: "" for the start page or
 * "/items/3". The server stores "/" for the start page.
 */
export interface OpenTab {
  projectId: string;
  path: string;
}

export function openTab(tabs: readonly OpenTab[], projectId: string): OpenTab[] {
  return tabs.some((tab) => tab.projectId === projectId)
    ? [...tabs]
    : [...tabs, { projectId, path: "" }];
}

export function closeTab(tabs: readonly OpenTab[], projectId: string): OpenTab[] {
  return tabs.filter((tab) => tab.projectId !== projectId);
}

/** Same array when nothing changes, so a state update with it does not render again. */
export function setTabPath(tabs: OpenTab[], projectId: string, path: string): OpenTab[] {
  const current = tabs.find((tab) => tab.projectId === projectId);
  if (!current || current.path === path) return tabs;
  return tabs.map((tab) => (tab.projectId === projectId ? { ...tab, path } : tab));
}

/** The saved tabs the user can still open (an app they lost or that has no frontend any more is dropped), once each. */
export function restoreTabs(
  saved: readonly SavedTab[],
  openableProjectIds: ReadonlySet<string>,
): OpenTab[] {
  const seen = new Set<string>();
  const tabs: OpenTab[] = [];
  for (const item of saved) {
    if (!openableProjectIds.has(item.projectId) || seen.has(item.projectId)) continue;
    seen.add(item.projectId);
    tabs.push({ projectId: item.projectId, path: item.lastPath === "/" ? "" : item.lastPath });
  }
  return tabs;
}

export function toSavedTabs(tabs: readonly OpenTab[]): SavedTab[] {
  return tabs.map((tab) => ({ projectId: tab.projectId, lastPath: tab.path || "/" }));
}
