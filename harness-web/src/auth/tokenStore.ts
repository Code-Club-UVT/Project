export interface AccountUser {
  id: string;
  username: string;
  email: string;
  emailConfirmed: boolean;
  createdAt: string;
}

export interface Session {
  accessToken: string;
  expiresAt: string;
  user: AccountUser;
}

export type SessionStatus = "loading" | "authenticated" | "anonymous";

interface State {
  status: SessionStatus;
  session: Session | null;
}

// The access token lives in memory only (never localStorage): a page reload gets a new one from the refresh cookie.
let state: State = { status: "loading", session: null };
const listeners = new Set<() => void>();

function set(next: State) {
  state = next;
  for (const listener of listeners) listener();
}

export const tokenStore = {
  getState: () => state,
  getAccessToken: () => state.session?.accessToken ?? null,
  setSession: (session: Session) => set({ status: "authenticated", session }),
  clear: () => set({ status: "anonymous", session: null }),
  subscribe: (listener: () => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
