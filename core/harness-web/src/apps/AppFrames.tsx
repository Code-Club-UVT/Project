import { useEffect, useRef, useState } from "react";
import { useMatch } from "react-router-dom";
import type { MyProject } from "../api/projects";
import { config } from "../config";
import { useAppBridge } from "./AppBridge";
import { appPathFromRoute, appUrl } from "./appUrl";
import { useOpenTabs } from "./OpenTabs";

const VISIBLE =
  "block h-[calc(100vh-16rem)] min-h-96 w-full rounded-lg border border-slate-300 bg-white dark:border-slate-700";

function AppFrame({
  app,
  initialPath,
  active,
}: {
  app: MyProject;
  initialPath: string;
  active: boolean;
}) {
  const { host } = useAppBridge();
  const ref = useRef<HTMLIFrameElement>(null);

  // Register with the host before the frame gets its address, so its first `ready` is answered.
  useEffect(() => {
    const frame = ref.current;
    if (!frame || !host) return;
    const unregister = host.register(app.slug, frame);
    frame.src = appUrl(config.basePath, app.slug, initialPath);
    return unregister;
  }, [host, app.slug, initialPath]);

  return <iframe ref={ref} title={app.name} className={active ? VISIBLE : "hidden"} />;
}

interface Opened {
  slug: string;
  initialPath: string;
}

/**
 * One iframe per open tab. A frame loads the first time its tab is shown and stays mounted (hidden) when the user goes
 * elsewhere, so the app keeps its state. Closing the tab removes the frame.
 */
export function AppFrames({ apps }: { apps: MyProject[] }) {
  const match = useMatch("/app/:slug/*");
  const active = match?.params.slug ?? null;
  const rest = match?.params["*"];
  const { tabs } = useOpenTabs();
  const [opened, setOpened] = useState<Opened[]>([]);

  useEffect(() => {
    const openSlugs = new Set(
      tabs.flatMap((tab) =>
        apps.filter((app) => app.projectId === tab.projectId).map((app) => app.slug),
      ),
    );
    setOpened((previous) => {
      const kept = previous.filter((item) => openSlugs.has(item.slug));
      if (active && openSlugs.has(active) && !kept.some((item) => item.slug === active)) {
        return [...kept, { slug: active, initialPath: appPathFromRoute(rest) }];
      }
      return kept.length === previous.length ? previous : kept;
    });
  }, [active, rest, apps, tabs]);

  return (
    <>
      {opened.map((item) => {
        const app = apps.find((candidate) => candidate.slug === item.slug);
        return app ? (
          <AppFrame
            key={item.slug}
            app={app}
            initialPath={item.initialPath}
            active={item.slug === active}
          />
        ) : null;
      })}
    </>
  );
}
