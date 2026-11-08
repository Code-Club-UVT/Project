import { useTranslation } from "react-i18next";
import { NavLink } from "react-router-dom";
import type { MyProject } from "../api/projects";
import { useAppBridge } from "./AppBridge";

const tab = (isActive: boolean) =>
  `rounded-t-md border-b-2 px-3 py-2 text-sm font-medium ${
    isActive
      ? "border-slate-900 dark:border-slate-100"
      : "border-transparent text-slate-600 hover:border-slate-300 dark:text-slate-400"
  }`;

/** Home and one tab per app (project with a frontend) the user can open. */
export function AppTabs({ apps }: { apps: MyProject[] }) {
  const { t } = useTranslation();
  const { lastPaths } = useAppBridge();

  return (
    <nav
      aria-label={t("apps.tabs")}
      className="flex flex-wrap gap-1 border-b border-slate-200 dark:border-slate-800"
    >
      <NavLink to="/" end className={({ isActive }) => tab(isActive)}>
        {t("apps.home")}
      </NavLink>
      {apps.map((app) => (
        <NavLink
          key={app.slug}
          to={`/app/${app.slug}${lastPaths[app.slug] ?? ""}`}
          title={t("apps.role", { role: app.roleName })}
          className={({ isActive }) => tab(isActive)}
        >
          {app.name}
        </NavLink>
      ))}
    </nav>
  );
}
