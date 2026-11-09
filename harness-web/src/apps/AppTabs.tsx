import { useTranslation } from "react-i18next";
import { NavLink, useMatch, useNavigate } from "react-router-dom";
import type { MyProject } from "../api/projects";
import { appRoute } from "./appUrl";
import { useOpenTabs } from "./OpenTabs";

const tab = (isActive: boolean) =>
  `rounded-t-md border-b-2 px-3 py-2 text-sm font-medium ${
    isActive
      ? "border-slate-900 dark:border-slate-100"
      : "border-transparent text-slate-600 hover:border-slate-300 dark:text-slate-400"
  }`;

/** Home and one tab per app the user has open (the others are in the menu on Home). */
export function AppTabs({ apps }: { apps: MyProject[] }) {
  const { t } = useTranslation();
  const { tabs, close } = useOpenTabs();
  const navigate = useNavigate();
  const activeSlug = useMatch("/app/:slug/*")?.params.slug;

  return (
    <nav
      aria-label={t("apps.tabs")}
      className="flex flex-wrap gap-1 border-b border-slate-200 dark:border-slate-800"
    >
      <NavLink to="/" end className={({ isActive }) => tab(isActive)}>
        {t("apps.home")}
      </NavLink>
      {tabs.map((open) => {
        const app = apps.find((candidate) => candidate.projectId === open.projectId);
        if (!app) return null;
        return (
          <span key={app.projectId} className="flex items-center">
            <NavLink
              to={appRoute(app.slug, open.path)}
              title={t("apps.role", { role: app.roleName })}
              className={({ isActive }) => tab(isActive)}
            >
              {app.name}
            </NavLink>
            <button
              type="button"
              aria-label={t("apps.close", { name: app.name })}
              title={t("apps.close", { name: app.name })}
              className="rounded px-1 text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-800"
              onClick={() => {
                close(app.projectId);
                if (activeSlug === app.slug) navigate("/");
              }}
            >
              ×
            </button>
          </span>
        );
      })}
    </nav>
  );
}
