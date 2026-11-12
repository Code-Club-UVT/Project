import { useMutation, useQueryClient } from "@tanstack/react-query";
import { authApi } from "../api/auth";
import { whoamiApi } from "../api/whoami";
import { tokenStore } from "./tokenStore";

export function useRegister() {
  return useMutation({ mutationFn: authApi.register });
}

export function useLogin() {
  return useMutation({
    mutationFn: authApi.login,
    onSuccess: (session) => tokenStore.setSession(session),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: authApi.logout,
    // Even if the call failed (offline, ...) the user wants out: forget the token and everything fetched with it.
    onSettled: () => {
      tokenStore.clear();
      queryClient.clear();
    },
  });
}

/** One-off calls through the gateway that show what the project token carries. */
export function useWhoami(kind: keyof typeof whoamiApi) {
  return useMutation({ mutationFn: whoamiApi[kind] });
}
