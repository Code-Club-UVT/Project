/**
 * Where an app is served: `<base path>apps/<slug>/` (the base path is `/dev/`, `/preprod/` or `/`). Apps use hash
 * routing, so a path inside the app goes after `#`.
 */
export function appUrl(basePath: string, slug: string, path?: string): string {
  const base = basePath.endsWith("/") ? basePath : `${basePath}/`;
  const inner = (path ?? "").replace(/^\/+/, "");
  return `${base}apps/${slug}/${inner ? `#/${inner}` : ""}`;
}

/** An app's icon: `icon.svg` at the root of the app (the harness shows a default one when it is missing). */
export function appIconUrl(basePath: string, slug: string): string {
  return `${appUrl(basePath, slug)}icon.svg`;
}

/** The harness route that shows an app, at an app-internal path ("" or "/items/3"): `/app/<slug>/items/3`. */
export function appRoute(slug: string, path = ""): string {
  const inner = path.replace(/^\/+/, "");
  return `/app/${slug}${inner ? `/${inner}` : ""}`;
}

/** The router's basename for a base path (`/dev/` gives `/dev`; the root gives no basename). */
export function routerBasename(basePath: string): string | undefined {
  const trimmed = basePath.replace(/\/+$/, "");
  return trimmed || undefined;
}

/** The app's inner path from the rest of the harness route (`/app/<slug>/<rest>`): "" or "/items/3". */
export function appPathFromRoute(rest: string | undefined): string {
  const inner = (rest ?? "").replace(/^\/+/, "");
  return inner ? `/${inner}` : "";
}
