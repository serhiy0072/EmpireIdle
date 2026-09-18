import { ApiError, type ProblemDetails } from "./problem";
import { fromAuthResponse, getSession, setSession, type AuthResponse } from "./session";

export { ApiError, isApiError } from "./problem";

export const API_URL = (import.meta.env.VITE_API_URL as string | undefined) ?? "";

export interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  /** Команди з IIdempotentRequest: повтор після обриву мережі має вернути той самий результат. */
  idempotent?: boolean;
  signal?: AbortSignal;
}

/** Одна ротація на всіх: шість паралельних 401 не мають зробити шість рефрешів. */
let refreshing: Promise<boolean> | null = null;

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options);

  // Сам рефреш не рефрешимо: інакше протухла пара зациклиться
  if (response.status === 401 && !path.startsWith("/api/auth/")) {
    if (await refreshOnce()) {
      return unwrap<T>(await send(path, options));
    }

    setSession(null);
  }

  return unwrap<T>(response);
}

export function apiGet<T>(path: string): Promise<T> {
  return api<T>(path);
}

export function apiPost<T>(path: string, body: unknown, options: RequestOptions = {}): Promise<T> {
  return api<T>(path, { ...options, method: "POST", body });
}

function send(path: string, options: RequestOptions): Promise<Response> {
  const session = getSession();
  const headers = new Headers({ Accept: "application/json" });

  if (session !== null) {
    headers.set("Authorization", `Bearer ${session.accessToken}`);
  }

  if (options.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  if (options.idempotent === true) {
    headers.set("Idempotency-Key", crypto.randomUUID());
  }

  return fetch(`${API_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  });
}

function refreshOnce(): Promise<boolean> {
  refreshing ??= rotate().finally(() => {
    refreshing = null;
  });

  return refreshing;
}

async function rotate(): Promise<boolean> {
  const session = getSession();

  if (session === null) return false;

  const response = await fetch(`${API_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify({ refreshToken: session.refreshToken }),
  });

  if (!response.ok) return false;

  setSession(fromAuthResponse((await response.json()) as AuthResponse));
  return true;
}

async function unwrap<T>(response: Response): Promise<T> {
  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  const payload = text === "" ? null : (JSON.parse(text) as unknown);

  if (!response.ok) {
    // Бек завжди віддає ProblemDetails, але падати на відповіді проксі не варто
    throw new ApiError(response.status, (payload as ProblemDetails | null) ?? { status: response.status });
  }

  return payload as T;
}
