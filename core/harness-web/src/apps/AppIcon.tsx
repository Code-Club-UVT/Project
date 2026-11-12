import { useState } from "react";
import { config } from "../config";
import { appIconUrl } from "./appUrl";

/** The app's `icon.svg`, or a default icon when the app has none (or it cannot be loaded). */
export function AppIcon({ slug, className = "h-10 w-10" }: { slug: string; className?: string }) {
  const [failed, setFailed] = useState(false);

  if (failed) {
    return (
      <svg
        viewBox="0 0 24 24"
        aria-hidden="true"
        className={`${className} rounded-md text-slate-500 dark:text-slate-400`}
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
      >
        <rect x="3" y="3" width="18" height="18" rx="4" />
        <rect x="7" y="7" width="4" height="4" rx="1" />
        <rect x="13" y="7" width="4" height="4" rx="1" />
        <rect x="7" y="13" width="4" height="4" rx="1" />
        <rect x="13" y="13" width="4" height="4" rx="1" />
      </svg>
    );
  }
  return (
    <img
      src={appIconUrl(config.basePath, slug)}
      alt=""
      className={`${className} rounded-md object-contain`}
      onError={() => setFailed(true)}
    />
  );
}
