// An app must not choose who the call is made as: the token is the harness's (and the gateway swaps it anyway).
const FORBIDDEN_REQUEST_HEADERS = new Set([
  "authorization",
  "cookie",
  "host",
  "content-length",
  "x-internal-key",
]);

/** The headers an app may send with a call, lowercased. */
export function sanitizeHeaders(
  headers: Record<string, string> | undefined,
): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [name, value] of Object.entries(headers ?? {})) {
    const key = name.toLowerCase();
    if (FORBIDDEN_REQUEST_HEADERS.has(key) || key.startsWith("x-user-")) continue;
    result[key] = value;
  }
  return result;
}

/** The headers of a response that an app gets to see: plain string values, never cookies. */
export function responseHeaders(headers: Record<string, unknown>): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [name, value] of Object.entries(headers)) {
    const key = name.toLowerCase();
    if (key === "set-cookie" || typeof value !== "string") continue;
    result[key] = value;
  }
  return result;
}
