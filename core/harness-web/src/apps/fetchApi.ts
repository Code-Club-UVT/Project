import { type AxiosResponse, isAxiosError } from "axios";
import type { AppRequest, AppResponse } from "platform-kit-web/sdk";
import { http } from "../api/http";
import { responseHeaders, sanitizeHeaders } from "./forwardHeaders";

function toAppResponse(id: string, response: AxiosResponse<unknown>): AppResponse {
  return {
    id,
    status: response.status,
    headers: responseHeaders(response.headers as unknown as Record<string, unknown>),
    body: typeof response.data === "string" ? response.data : "",
  };
}

/**
 * Makes an app's API call as the signed-in user: `/api/<slug>/<path>` through the shared `http` instance, so the token,
 * the single shared refresh on a 401 and the retries of safe requests all apply. A response with an error status is a
 * result for the app, not a failure; only a call that got no response at all throws.
 */
export async function fetchApi(request: AppRequest, slug: string): Promise<AppResponse> {
  try {
    const response = await http.request<string>({
      url: `/${slug}/${request.path.replace(/^\/+/, "")}`,
      method: request.method,
      headers: sanitizeHeaders(request.headers),
      data: request.body,
      responseType: "text",
      transformResponse: [(data: unknown) => data],
    });
    return toAppResponse(request.id, response);
  } catch (error) {
    if (isAxiosError(error) && error.response) return toAppResponse(request.id, error.response);
    throw error;
  }
}
