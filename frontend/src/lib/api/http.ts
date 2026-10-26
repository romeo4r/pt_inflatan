/**
 * Thin HTTP layer over the Mundialito API. Everything the UI needs to know about the wire
 * format lives here: base URL, JSON, ProblemDetails (RFC 9457), Idempotency-Key and If-Match.
 * Pages never call fetch directly.
 */

export const API_BASE_URL = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000").replace(/\/$/, "");

/** Error shape produced by the API for 4xx/5xx responses, plus network failures. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly detail: string;
  readonly fieldErrors: Record<string, string[]>;
  readonly correlationId: string | null;

  constructor(init: {
    status: number;
    code: string;
    detail: string;
    fieldErrors?: Record<string, string[]>;
    correlationId?: string | null;
  }) {
    super(init.detail);
    this.name = "ApiError";
    this.status = init.status;
    this.code = init.code;
    this.detail = init.detail;
    this.fieldErrors = init.fieldErrors ?? {};
    this.correlationId = init.correlationId ?? null;
  }

  /** First message for a given field (camelCase as the API sends it), if any. */
  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined | null>;
  /** Sent as Idempotency-Key. Required by the API on every POST that creates something. */
  idempotencyKey?: string;
  /** Sent as If-Match for optimistic concurrency. */
  etag?: string | null;
  signal?: AbortSignal;
}

export function buildQuery(query: RequestOptions["query"]): string {
  if (!query) return "";
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === "") continue;
    params.set(key, String(value));
  }
  const text = params.toString();
  return text ? `?${text}` : "";
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json" };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  if (options.idempotencyKey) headers["Idempotency-Key"] = options.idempotencyKey;
  if (options.etag) headers["If-Match"] = options.etag;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}${buildQuery(options.query)}`, {
      method: options.method ?? "GET",
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new ApiError({
      status: 0,
      code: "Network.Unreachable",
      detail: "No se pudo conectar con el servidor. Verifique que la API esté en ejecución.",
    });
  }

  if (response.status === 204) return undefined as T;

  const text = await response.text();
  const json = text ? safeParse(text) : null;

  if (!response.ok) {
    throw toApiError(response, json);
  }

  return json as T;
}

/** Strong ETag of the last GET, so forms can send If-Match when they write. */
export async function requestWithETag<T>(path: string): Promise<{ data: T; etag: string | null }> {
  const response = await fetch(`${API_BASE_URL}${path}`, { headers: { Accept: "application/json" } });
  const json = safeParse(await response.text());
  if (!response.ok) throw toApiError(response, json);
  return { data: json as T, etag: response.headers.get("ETag") };
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function toApiError(response: Response, json: unknown): ApiError {
  const problem = (json ?? {}) as {
    code?: string;
    detail?: string;
    title?: string;
    errors?: Record<string, string[]>;
    correlationId?: string;
  };

  return new ApiError({
    status: response.status,
    code: problem.code ?? `Http.${response.status}`,
    detail: problem.detail ?? problem.title ?? `Error ${response.status} al llamar a la API.`,
    fieldErrors: problem.errors,
    correlationId: problem.correlationId ?? response.headers.get("X-Correlation-Id"),
  });
}

/**
 * One key per submission attempt; reused on retry of the same form so the API replays instead of
 * duplicating. Call it from event handlers only: Next.js prerenders client components and
 * rejects non-deterministic values during render.
 */
export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

/** Returns the current key or mints one; forms keep the key until the submission succeeds. */
export function ensureIdempotencyKey(current: string | null): string {
  return current ?? newIdempotencyKey();
}
