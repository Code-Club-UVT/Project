import type { MyProject } from "../api/projects";

// System projects have no screen of their own in the harness.
const HIDDEN_SLUGS = new Set(["core", "identity"]);

/** The projects that get a tab: the ones with a frontend, except the system projects, sorted by name. */
export function visibleApps(projects: readonly MyProject[]): MyProject[] {
  return projects
    .filter((project) => project.hasFrontend && !HIDDEN_SLUGS.has(project.slug))
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: "base" }));
}
