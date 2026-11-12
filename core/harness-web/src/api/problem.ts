import { isAxiosError } from "axios";

export interface Problem {
  status: number | null;
  /** The backend's error code (Auth.InvalidCredentials, ...), or "Network" when no response arrived. */
  code: string;
  detail: string | null;
}

/** Turns anything thrown by an API call into a ProblemDetails-shaped value the UI can translate. */
export function toProblem(error: unknown): Problem {
  if (isAxiosError(error)) {
    if (!error.response) return { status: null, code: "Network", detail: error.message };
    const data = error.response.data as
      | { code?: string; detail?: string; title?: string }
      | undefined;
    return {
      status: error.response.status,
      code: data?.code ?? `Http.${error.response.status}`,
      detail: data?.detail ?? data?.title ?? null,
    };
  }
  return { status: null, code: "Unknown", detail: error instanceof Error ? error.message : null };
}
