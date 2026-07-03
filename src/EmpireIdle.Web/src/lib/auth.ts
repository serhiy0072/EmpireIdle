import { apiPost } from "./api";

const TOKEN_KEY = "empireidle.accessToken";
const PLAYER_KEY = "empireidle.playerId";

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  playerId: string;
}

export async function login(email: string, password: string, remember = true): Promise<AuthResponse> {
  const auth = await apiPost<AuthResponse>("/login", { email, password });

  const store = remember ? localStorage : sessionStorage;
  const other = remember ? sessionStorage : localStorage;
  other.removeItem(TOKEN_KEY);
  other.removeItem(PLAYER_KEY);
  store.setItem(TOKEN_KEY, auth.accessToken);
  store.setItem(PLAYER_KEY, auth.playerId);
  return auth;
}

function read(key: string): string | null {
  return localStorage.getItem(key) ?? sessionStorage.getItem(key);
}

export function getToken(): string | null {
  return read(TOKEN_KEY);
}

export function getPlayerId(): string | null {
  return read(PLAYER_KEY);
}

export function logout(): void {
  [localStorage, sessionStorage].forEach((s) => {
    s.removeItem(TOKEN_KEY);
    s.removeItem(PLAYER_KEY);
  });
}