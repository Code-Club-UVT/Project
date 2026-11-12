import { useSyncExternalStore } from "react";
import { tokenStore } from "./tokenStore";

export function useSession() {
  return useSyncExternalStore(tokenStore.subscribe, tokenStore.getState);
}
