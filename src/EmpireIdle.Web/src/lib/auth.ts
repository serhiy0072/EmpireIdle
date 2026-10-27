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

/**
 * Вихід: сесія зникає локально одразу, а refresh-токен відкликається на сервері — інакше збережена
 * копія жила б до кінця строку. Збій мережі вихід не блокує: локально гравець уже вийшов.
 */
export async function logout(): Promise<void> {
  const refreshToken = getSession()?.refreshToken ?? null;

  setSession(null);

  if (refreshToken === null) return;

  try {
    await apiPost<void>("/api/auth/logout", { refreshToken });
  } catch {
    // Токен однаково спливе сам; гравця вже виведено
  }
}
