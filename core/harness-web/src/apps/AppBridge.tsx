import { type AppHost, createAppHost } from "platform-kit-web/sdk";
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
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import type { MyProject } from "../api/projects";
import { tokenStore } from "../auth/tokenStore";
import { useSession } from "../auth/useSession";
import { appPathFromRoute } from "./appUrl";
import { fetchApi } from "./fetchApi";

interface Bridge {
  host: AppHost | null;
  /** The last path each app reported, so a tab link can return to where the user was. */
  lastPaths: Record<string, string>;
}

const BridgeContext = createContext<Bridge>({ host: null, lastPaths: {} });

export function useAppBridge(): Bridge {
  return useContext(BridgeContext);
}

const currentTheme = () => (document.documentElement.classList.contains("dark") ? "dark" : "light");

/**
 * Owns the one bridge host of the harness: answers the apps in the iframes (init, API calls), keeps the address in step
 * with the app's path, and tells every app about role, theme, language and logout changes.
 */
export function AppBridgeProvider({ apps, children }: { apps: MyProject[]; children: ReactNode }) {
  const { session } = useSession();
  const { i18n } = useTranslation();
  const navigate = useNavigate();
  const [host, setHost] = useState<AppHost | null>(null);
  const [lastPaths, setLastPaths] = useState<Record<string, string>>({});

  // The host is created once; it reads the current values through this ref, and navigate is not stable across routes.
  const latest = useRef({ session, apps, language: i18n.resolvedLanguage ?? "en", navigate });
  latest.current = { session, apps, language: i18n.resolvedLanguage ?? "en", navigate };

  const onNavigated = useCallback((slug: string, path: string) => {
    const inner = appPathFromRoute(path);
    setLastPaths((previous) =>
      previous[slug] === inner ? previous : { ...previous, [slug]: inner },
    );
    latest.current.navigate(`/app/${slug}${inner}`, { replace: true });
  }, []);

  useEffect(() => {
    const created = createAppHost({
      getInit: (slug) => {
        const { session: current, apps: list, language } = latest.current;
        return {
          user: { id: current?.user.id ?? "", username: current?.user.username ?? "" },
          role: list.find((app) => app.slug === slug)?.roleName ?? null,
          slug,
          locale: language,
          theme: currentTheme(),
        };
      },
      fetchApi,
      onNavigated,
    });
    setHost(created);
    return () => {
      created.dispose();
      setHost(null);
    };
  }, [onNavigated]);

  // Logout: tell the apps before the frames are removed (the store notifies before React renders).
  useEffect(() => {
    if (!host) return;
    let wasAuthenticated = tokenStore.getState().status === "authenticated";
    return tokenStore.subscribe(() => {
      const authenticated = tokenStore.getState().status === "authenticated";
      if (wasAuthenticated && !authenticated) host.broadcast("logout");
      wasAuthenticated = authenticated;
    });
  }, [host]);

  // Theme: the theme provider toggles the `dark` class on <html>.
  useEffect(() => {
    if (!host) return;
    const observer = new MutationObserver(() => host.broadcast("theme", currentTheme()));
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
    return () => observer.disconnect();
  }, [host]);

  useEffect(() => {
    if (!host) return;
    const onChange = (language: string) => host.broadcast("locale", language);
    i18n.on("languageChanged", onChange);
    return () => i18n.off("languageChanged", onChange);
  }, [host, i18n]);

  // Role: when a refetch shows another role in a project, the apps get `role-changed` with {slug, role}.
  const roles = useRef<Record<string, string>>({});
  useEffect(() => {
    const next = Object.fromEntries(apps.map((app) => [app.slug, app.roleName]));
    if (host) {
      for (const [slug, role] of Object.entries(next)) {
        if (slug in roles.current && roles.current[slug] !== role) {
          host.broadcast("role-changed", { slug, role });
        }
      }
    }
    roles.current = next;
  }, [apps, host]);

  const value = useMemo(() => ({ host, lastPaths }), [host, lastPaths]);
  return <BridgeContext.Provider value={value}>{children}</BridgeContext.Provider>;
}
