import { useTranslation } from "react-i18next";
import { LANGUAGES } from "../i18n";
import { type Theme, useTheme } from "../theme/ThemeProvider";

const THEMES: Theme[] = ["light", "dark", "system"];

const selectStyle =
  "rounded-md border border-slate-300 bg-white px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-900";

export function Header() {
  const { t, i18n } = useTranslation();
  const { theme, setTheme } = useTheme();

  return (
    <header className="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 className="text-2xl font-bold">{t("app.title")}</h1>
        <p className="text-sm text-slate-600 dark:text-slate-400">{t("app.subtitle")}</p>
      </div>
      <div className="flex gap-3">
        <label className="text-sm">
          <span className="sr-only">{t("theme.label")}</span>
          <select
            className={selectStyle}
            value={theme}
            onChange={(e) => setTheme(e.target.value as Theme)}
          >
            {THEMES.map((value) => (
              <option key={value} value={value}>
                {t(`theme.${value}`)}
              </option>
            ))}
          </select>
        </label>
        <label className="text-sm">
          <span className="sr-only">{t("language.label")}</span>
          <select
            className={selectStyle}
            value={i18n.resolvedLanguage}
            onChange={(e) => i18n.changeLanguage(e.target.value)}
          >
            {LANGUAGES.map((value) => (
              <option key={value} value={value}>
                {t(`language.${value}`)}
              </option>
            ))}
          </select>
        </label>
      </div>
    </header>
  );
}
