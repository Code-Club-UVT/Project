import type { Session } from "../auth/tokenStore";
import { http } from "./http";

export interface RegisterInput {
  username: string;
  email: string;
  password: string;
  firstName?: string;
  lastName?: string;
}

export interface LoginInput {
  identifier: string;
  password: string;
}

const noRefresh = { skipAuthRefresh: true } as const;

export const authApi = {
  register: (input: RegisterInput) =>
    http.post<unknown>("/identity/auth/register", input, noRefresh).then((r) => r.data),
  login: (input: LoginInput) =>
    http.post<Session>("/identity/auth/login", input, noRefresh).then((r) => r.data),
  logout: () => http.post("/identity/auth/logout", null, noRefresh).then(() => undefined),
};
