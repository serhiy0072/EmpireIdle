import { apiPost } from "./api";
import { fromAuthResponse, getSession, setSession, type AuthResponse } from "./session";

export type { AuthResponse, Session } from "./session";

export async function login(email: string, password: string, remember = true): Promise<AuthResponse> {
  const auth = await apiPost<AuthResponse>("/api/auth/login", { email, password });

  setSession(fromAuthResponse(auth), remember);
  return auth;
}

export async function register(
  userName: string,
  email: string,
  password: string,
  remember = true,
): Promise<AuthResponse> {
  const auth = await apiPost<AuthResponse>("/api/auth/register", { userName, email, password });

  setSession(fromAuthResponse(auth), remember);
  return auth;
}

export function getPlayerId(): string | null {
  return getSession()?.playerId ?? null;
}

export function logout(): void {
  setSession(null);
}
