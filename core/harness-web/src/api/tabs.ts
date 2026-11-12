import { http } from "./http";

/** An app the user had open (`GET/PUT /core/users/me/tabs`): the project and where they were inside its app. */
export interface SavedTab {
  projectId: string;
  /** An app-internal path starting with "/" ("/" is the app's start page). */
  lastPath: string;
}

export const tabsApi = {
  mine: () => http.get<SavedTab[]>("/core/users/me/tabs").then((r) => r.data),
  /** Replaces the whole saved list. */
  save: (tabs: SavedTab[]) =>
    http.put<SavedTab[]>("/core/users/me/tabs", { tabs }).then((r) => r.data),
};
