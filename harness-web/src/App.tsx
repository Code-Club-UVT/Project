import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { HashRouter, Navigate, Route, Routes, useParams } from "react-router-dom";
import { AppBridgeProvider } from "./apps/AppBridge";
import { AppFrames } from "./apps/AppFrames";
import { AppTabs } from "./apps/AppTabs";
import { visibleApps } from "./apps/visibleApps";
import { useMyProjects } from "./auth/useMyProjects";
import { useSession } from "./auth/useSession";
import { Header } from "./components/Header";
import { Home } from "./components/Home";

/**
 * `/app/<slug>/...`: the iframe itself is drawn by AppFrames (so it survives tab switches); this route only sends the
 * user home when the slug is not one of their apps.
 */
function AppRoute({ slugs, loading }: { slugs: string[]; loading: boolean }) {
  const { t } = useTranslation();
  const { slug } = useParams();
  const { status } = useSession();

  if (status === "loading" || (status === "authenticated" && loading)) {
    return <p className="text-sm">{t("common.loading")}</p>;
  }
  if (status !== "authenticated" || !slug || !slugs.includes(slug)) {
    return <Navigate to="/" replace />;
  }
  return null;
}

function Shell() {
  const { t } = useTranslation();
  const { status } = useSession();
  const projects = useMyProjects();
  const apps = useMemo(() => visibleApps(projects.data ?? []), [projects.data]);
  const slugs = useMemo(() => apps.map((app) => app.slug), [apps]);

  return (
    <AppBridgeProvider apps={apps}>
      <main className="mx-auto max-w-5xl space-y-6 px-4 py-8">
        <Header />
        {status === "authenticated" && <AppTabs apps={apps} />}
        {projects.isError && <p className="text-sm text-red-600">{t("apps.loadError")}</p>}
        <Routes>
          <Route path="/" element={<Home />} />
          <Route
            path="/app/:slug/*"
            element={<AppRoute slugs={slugs} loading={projects.isPending} />}
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
        {status === "authenticated" && <AppFrames apps={apps} />}
      </main>
    </AppBridgeProvider>
  );
}

export function App() {
  return (
    <HashRouter>
      <Shell />
    </HashRouter>
  );
}
