import { useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { BrowserRouter, Navigate, Route, Routes, useParams } from "react-router-dom";
import type { MyProject } from "./api/projects";
import { AppBridgeProvider } from "./apps/AppBridge";
import { AppFrames } from "./apps/AppFrames";
import { AppTabs } from "./apps/AppTabs";
import { routerBasename } from "./apps/appUrl";
import { OpenTabsProvider, useOpenTabs } from "./apps/OpenTabs";
import { visibleApps } from "./apps/visibleApps";
import { useMyProjects } from "./auth/useMyProjects";
import { useSession } from "./auth/useSession";
import { Header } from "./components/Header";
import { Home } from "./components/Home";
import { config } from "./config";

/**
 * `/app/<slug>/...`: the iframe itself is drawn by AppFrames (so it survives tab switches). This route opens the app's
 * tab if it is not open yet (a link or a bookmark), and sends the user home when the slug is not one of their apps.
 */
function AppRoute({ apps, loading }: { apps: MyProject[]; loading: boolean }) {
  const { t } = useTranslation();
  const { slug } = useParams();
  const { status } = useSession();
  const { open, ready } = useOpenTabs();
  const app = apps.find((candidate) => candidate.slug === slug);
  const projectId = app?.projectId;

  useEffect(() => {
    if (ready && projectId) open(projectId);
  }, [ready, projectId, open]);

  if (status === "loading" || (status === "authenticated" && (loading || !ready))) {
    return <p className="text-sm">{t("common.loading")}</p>;
  }
  if (status !== "authenticated" || !app) {
    return <Navigate to="/" replace />;
  }
  return null;
}

function Shell() {
  const { t } = useTranslation();
  const { status } = useSession();
  const projects = useMyProjects();
  const apps = useMemo(() => visibleApps(projects.data ?? []), [projects.data]);

  return (
    <AppBridgeProvider apps={apps}>
      <OpenTabsProvider apps={apps} appsReady={projects.isSuccess}>
        <main className="mx-auto max-w-5xl space-y-6 px-4 py-8">
          <Header />
          {status === "authenticated" && <AppTabs apps={apps} />}
          {projects.isError && <p className="text-sm text-red-600">{t("apps.loadError")}</p>}
          <Routes>
            <Route path="/" element={<Home apps={status === "authenticated" ? apps : []} />} />
            <Route
              path="/app/:slug/*"
              element={<AppRoute apps={apps} loading={projects.isPending} />}
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
          {status === "authenticated" && <AppFrames apps={apps} />}
        </main>
      </OpenTabsProvider>
    </AppBridgeProvider>
  );
}

export function App() {
  return (
    <BrowserRouter basename={routerBasename(config.basePath)}>
      <Shell />
    </BrowserRouter>
  );
}
