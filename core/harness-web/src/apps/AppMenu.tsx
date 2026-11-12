import { useTranslation } from "react-i18next";
import { Link } from "react-router-dom";
import type { MyProject } from "../api/projects";
import { AppIcon } from "./AppIcon";
import { appRoute } from "./appUrl";
import { useOpenTabs } from "./OpenTabs";

/** Every app the user can open: its icon, name and role. Choosing one opens it in a tab (or goes to its open tab). */
export function AppMenu({ apps }: { apps: MyProject[] }) {
  const { t } = useTranslation();
  const { tabs } = useOpenTabs();

  if (apps.length === 0) return null;
  return (
    <section aria-label={t("apps.menu")} className="space-y-3">
      <h2 className="text-lg font-semibold">{t("apps.menu")}</h2>
      <ul className="grid gap-3 sm:grid-cols-2 md:grid-cols-3">
        {apps.map((app) => (
          <li key={app.projectId}>
            <Link
              to={appRoute(app.slug, tabs.find((tab) => tab.projectId === app.projectId)?.path)}
              className="flex items-center gap-3 rounded-lg border border-slate-200 p-3 hover:border-slate-400 dark:border-slate-800 dark:hover:border-slate-600"
            >
              <AppIcon slug={app.slug} />
              <span className="min-w-0">
                <span className="block truncate font-medium">{app.name}</span>
                <span className="block truncate text-sm text-slate-600 dark:text-slate-400">
                  {t("apps.role", { role: app.roleName })}
                </span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
