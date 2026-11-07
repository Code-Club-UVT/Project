export interface HarnessConfig {
  /** Where the gateway's /api is: relative to the page origin (/dev/api, /api) or absolute for a local gateway. */
  apiUrl: string;
  /** Slug of the mock project, used by the "mock whoami" button. */
  mockSlug: string;
}

declare global {
  interface Window {
    __HARNESS_CONFIG__?: Partial<HarnessConfig>;
  }
}

// config.js is loaded before the app (see index.html); the container rewrites it from its environment variables.
const raw = window.__HARNESS_CONFIG__ ?? {};

export const config: HarnessConfig = {
  apiUrl: (raw.apiUrl ?? "/api").replace(/\/+$/, ""),
  mockSlug: raw.mockSlug ?? "mock",
};
