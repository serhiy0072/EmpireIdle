import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { startRealtime } from "../lib/realtime/connection";
import { useSession } from "./useSession";

/**
 * Одне підключення на сесію. Залежність лише від playerId: ротація токена
 * міняє об'єкт сесії, але перепідключатись через це не треба.
 */
export function useRealtime(): void {
  const session = useSession();
  const queryClient = useQueryClient();
  const playerId = session?.playerId ?? null;

  useEffect(() => {
    if (playerId === null) return;

    return startRealtime(queryClient, playerId);
  }, [playerId, queryClient]);
}
