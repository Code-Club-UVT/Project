/**
 * Where an app is served: `<base path>apps/<slug>/` (the base path is `/dev/`, `/preprod/` or `/`). Apps use hash
 * routing, so a path inside the app goes after `#`.
 */
export function appUrl(basePath: string, slug: string, path?: string): string {
  const base = basePath.endsWith("/") ? basePath : `${basePath}/`;
  const inner = (path ?? "").replace(/^\/+/, "");
  return `${base}apps/${slug}/${inner ? `#/${inner}` : ""}`;
}

/** The app's inner path from the rest of the harness route (`/app/<slug>/<rest>`): "" or "/items/3". */
export function appPathFromRoute(rest: string | undefined): string {
  const inner = (rest ?? "").replace(/^\/+/, "");
  return inner ? `/${inner}` : "";
}
