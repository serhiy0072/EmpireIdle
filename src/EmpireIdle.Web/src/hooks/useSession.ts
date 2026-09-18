import { useSyncExternalStore } from "react";
import { getSession, subscribe, type Session } from "../lib/session";

/** Сесія живе поза React: її перезаписує клієнт API під час ротації токена. */
export function useSession(): Session | null {
  return useSyncExternalStore(subscribe, getSession);
}
