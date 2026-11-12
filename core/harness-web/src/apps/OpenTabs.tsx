import { useQuery } from "@tanstack/react-query";
import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import type { MyProject } from "../api/projects";
import { tabsApi } from "../api/tabs";
import { useSession } from "../auth/useSession";
import { useAppBridge } from "./AppBridge";
import { createDebouncedSaver } from "./debouncedSaver";
import { closeTab, type OpenTab, openTab, restoreTabs, setTabPath, toSavedTabs } from "./tabList";

const SAVE_DELAY_MS = 800;

interface OpenTabsValue {
  /** The apps open as tabs, in order. */
  tabs: OpenTab[];
  /** True once the saved tabs are loaded (or the user is not logged in). */
  ready: boolean;
  open: (projectId: string) => void;
  close: (projectId: string) => void;
}

const OpenTabsContext = createContext<OpenTabsValue>({
  tabs: [],
  ready: false,
  open: () => {},
  close: () => {},
});

export function useOpenTabs(): OpenTabsValue {
  return useContext(OpenTabsContext);
}

/**
 * The tabs the user has open. They come back from the server after login, are filtered by the apps the user can open
 * now, follow the path each app reports, and are saved (the whole list, after a short quiet period) when they change.
 */
export function OpenTabsProvider({
  apps,
  appsReady,
  children,
}: {
  apps: MyProject[];
  appsReady: boolean;
  children: ReactNode;
}) {
  const { status, session } = useSession();
  const { lastPaths } = useAppBridge();
  const userId = status === "authenticated" ? (session?.user.id ?? null) : null;
  const [tabs, setTabs] = useState<OpenTab[]>([]);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  const saved = useQuery({
    queryKey: ["my-tabs", userId],
    queryFn: tabsApi.mine,
    enabled: userId !== null,
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });

  const saver = useMemo(
    () =>
      createDebouncedSaver(
        (value: ReturnType<typeof toSavedTabs>) => void tabsApi.save(value).catch(() => {}),
        SAVE_DELAY_MS,
      ),
    [],
  );

  const appsRef = useRef(apps);
  appsRef.current = apps;

  // Restore once per login, when both the saved tabs and the user's apps are known.
  useEffect(() => {
    if (userId === null) {
      saver.cancel();
      setTabs([]);
      setLoadedFor(null);
      return;
    }
    if (loadedFor === userId || !saved.isSuccess || !appsReady) return;
    const restored = restoreTabs(saved.data, new Set(apps.map((app) => app.projectId)));
    saver.markSaved(toSavedTabs(restored));
    // A deep link may have opened an app before the saved list arrived: keep it.
    setTabs((current) =>
      restored.concat(
        current.filter((tab) => !restored.some((r) => r.projectId === tab.projectId)),
      ),
    );
    setLoadedFor(userId);
  }, [userId, loadedFor, saved.isSuccess, saved.data, appsReady, apps, saver]);

  // Follow the path each app reports.
  useEffect(() => {
    setTabs((current) => {
      let next = current;
      for (const [slug, path] of Object.entries(lastPaths)) {
        const app = appsRef.current.find((candidate) => candidate.slug === slug);
        if (app) next = setTabPath(next, app.projectId, path);
      }
      return next;
    });
  }, [lastPaths]);

  // Save after every change, once the saved list has been loaded (so the restore itself is not saved back).
  useEffect(() => {
    if (userId !== null && loadedFor === userId) saver.schedule(toSavedTabs(tabs));
  }, [tabs, userId, loadedFor, saver]);

  // Leaving the page: do not wait for the quiet period.
  useEffect(() => {
    const onHide = () => {
      if (document.visibilityState === "hidden") saver.flush();
    };
    document.addEventListener("visibilitychange", onHide);
    return () => {
      document.removeEventListener("visibilitychange", onHide);
      saver.cancel();
    };
  }, [saver]);

  const open = useCallback(
    (projectId: string) => setTabs((current) => openTab(current, projectId)),
    [],
  );
  const close = useCallback(
    (projectId: string) => setTabs((current) => closeTab(current, projectId)),
    [],
  );

  const value = useMemo(
    () => ({ tabs, ready: userId === null || loadedFor === userId, open, close }),
    [tabs, userId, loadedFor, open, close],
  );
  return <OpenTabsContext.Provider value={value}>{children}</OpenTabsContext.Provider>;
}
