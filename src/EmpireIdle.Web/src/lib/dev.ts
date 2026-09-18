import { api } from "./api";

/** Дев-сід. Маршрут існує лише коли API піднято в Development. */
export function seedAccount(playerId: string): Promise<void> {
  return api<void>(`/api/dev/seed/${playerId}`, { method: "POST" });
}
