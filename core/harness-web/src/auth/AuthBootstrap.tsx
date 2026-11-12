import { type ReactNode, useEffect } from "react";
import { refreshSession } from "../api/http";
import { tokenStore } from "./tokenStore";

/** On start, tries to restore the session from the refresh cookie. Without a valid cookie the user is anonymous. */
export function AuthBootstrap({ children }: { children: ReactNode }) {
  useEffect(() => {
    refreshSession().catch(() => tokenStore.clear());
  }, []);
  return children;
}
