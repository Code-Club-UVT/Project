import { useQuery } from "@tanstack/react-query";
import { projectsApi } from "../api/projects";
import { useSession } from "./useSession";

/** The projects the signed-in user is a member of (and their role in each). Empty until logged in. */
export function useMyProjects() {
  const { status, session } = useSession();
  return useQuery({
    queryKey: ["my-projects", session?.user.id],
    queryFn: projectsApi.mine,
    enabled: status === "authenticated",
  });
}
