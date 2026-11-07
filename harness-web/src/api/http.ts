import axios, { type AxiosError, type AxiosRequestConfig } from "axios";
import { type Session, tokenStore } from "../auth/tokenStore";
import { config } from "../config";

declare module "axios" {
  interface AxiosRequestConfig {
    /** Allow retrying a non-idempotent request (POST, ...) after a network error or 502/503/504/429. Off by default. */
    retryable?: boolean;
    /** Never try to refresh the session after a 401 for this request (the auth endpoints themselves). */
    skipAuthRefresh?: boolean;
    _retryCount?: number;
    _authRetried?: boolean;
  }
}

const MAX_RETRIES = 3;
const BASE_DELAY_MS = 300;
const SAFE_METHODS = new Set(["get", "head", "options"]);
const RETRY_STATUSES = new Set([429, 502, 503, 504]);

const common = { baseURL: config.apiUrl, withCredentials: true, timeout: 15_000 };

/** The instance every API call uses: adds the access token, refreshes it on a 401, retries transient failures. */
export const http = axios.create(common);

// Used for the refresh call only, so it never goes through the interceptors below (no loop, no token attached).
const bare = axios.create(common);

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

function retryDelay(error: AxiosError, attempt: number): number {
  const header = error.response?.headers?.["retry-after"];
  const seconds = Number(header);
  if (header && Number.isFinite(seconds)) return Math.min(seconds, 10) * 1000;
  return BASE_DELAY_MS * 2 ** attempt + Math.random() * 200;
}

function shouldRetry(error: AxiosError, request: AxiosRequestConfig): boolean {
  if ((request._retryCount ?? 0) >= MAX_RETRIES) return false;
  const safe = SAFE_METHODS.has((request.method ?? "get").toLowerCase()) || request.retryable;
  if (!safe) return false;
  // No response: network error or timeout. A cancelled request is not retried.
  if (!error.response) return error.code !== "ERR_CANCELED";
  return RETRY_STATUSES.has(error.response.status);
}

let refreshing: Promise<Session> | null = null;

/**
 * Gets a new access token from the refresh cookie. Calls made at the same time share one request: a second refresh with
 * the cookie that the first one just rotated would look like token theft to identity and revoke the whole session.
 */
export function refreshSession(): Promise<Session> {
  refreshing ??= bare
    .post<Session>("/identity/auth/refresh")
    .then((response) => {
      tokenStore.setSession(response.data);
      return response.data;
    })
    .finally(() => {
      refreshing = null;
    });
  return refreshing;
}

http.interceptors.request.use((request) => {
  const token = tokenStore.getAccessToken();
  if (token && !request.headers.Authorization) request.headers.Authorization = `Bearer ${token}`;
  return request;
});

http.interceptors.response.use(undefined, async (error: AxiosError) => {
  const request = error.config;
  if (!request) throw error;

  // 1. Expired access token: refresh once, then repeat the request with the new token.
  if (error.response?.status === 401 && !request.skipAuthRefresh && !request._authRetried) {
    request._authRetried = true;
    try {
      const session = await refreshSession();
      request.headers.Authorization = `Bearer ${session.accessToken}`;
      return http.request(request);
    } catch {
      // The refresh cookie is gone or was rejected: the session is over.
      tokenStore.clear();
      throw error;
    }
  }

  // 2. Transient failure: wait and send the same request again.
  if (shouldRetry(error, request)) {
    const attempt = request._retryCount ?? 0;
    request._retryCount = attempt + 1;
    await sleep(retryDelay(error, attempt));
    return http.request(request);
  }

  throw error;
});
