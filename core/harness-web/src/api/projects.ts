import { http } from "./http";

/** A project the user can open (`GET /core/users/me/projects`). The URLs stay internal: only these flags are sent. */
export interface MyProject {
  projectId: string;
  slug: string;
  name: string;
  description: string | null;
  roleId: string;
  roleName: string;
  hasFrontend: boolean;
  hasBackend: boolean;
}

export const projectsApi = {
  mine: () => http.get<MyProject[]>("/core/users/me/projects").then((r) => r.data),
};
