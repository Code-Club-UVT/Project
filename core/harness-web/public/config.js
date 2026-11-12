// Runtime settings. The container overwrites this file at start from its environment variables
// (HARNESS_API_URL, HARNESS_MOCK_SLUG); this copy is what `npm run dev` uses.
window.__HARNESS_CONFIG__ = { apiUrl: "/api", mockSlug: "mock" };
