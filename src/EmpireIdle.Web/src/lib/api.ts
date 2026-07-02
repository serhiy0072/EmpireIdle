import { getToken } from "./auth";

const API_URL = import.meta.env.VITE_API_URL as string;

export class ApiError extends Error {
  status: number;
  problem: unknown;

  constructor(status: number, problem: unknown) {
    super((problem as { title?: string })?.title ?? `Запит провалився (${status})`);
    this.status = status;
    this.problem = problem;
  }
}

export async function apiPost<T>(path: string, body: unknown): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    throw new ApiError(res.status, problem);
  }

  return res.json() as Promise<T>;
}

export async function apiGet<T>(path: string): Promise<T> {
  const token = getToken();
  const res = await fetch(`${API_URL}${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });

  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    throw new ApiError(res.status, problem);
  }

  return res.json() as Promise<T>;
}