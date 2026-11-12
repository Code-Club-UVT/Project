import { config } from "../config";
import { http } from "./http";

// These go through the gateway with the edge token, so every call also checks that the gateway can get a project token
// from identity (core's own project token for the first two, the mock project's for the third).
export const whoamiApi = {
  profile: () => http.get<unknown>("/core/users/me").then((r) => r.data),
  projects: () => http.get<unknown>("/core/users/me/projects").then((r) => r.data),
  mock: () => http.get<unknown>(`/${config.mockSlug}/whoami`).then((r) => r.data),
};
