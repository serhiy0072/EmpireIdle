import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { useEffect } from "react";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";

export type PlayerSettingsView = components["schemas"]["PlayerSettingsView"];
export type CheckInView = components["schemas"]["CheckInView"];

/** Мова, поки налаштування не приїхали: мова, якою написані конфіги. */
export const DEFAULT_LANGUAGE = "uk";

export function usePlayerSettings(playerId: string): UseQueryResult<PlayerSettingsView> {
  return useQuery({
    queryKey: queryKeys.settings(playerId),
    queryFn: () => api<PlayerSettingsView>(`/api/player/${playerId}/settings`),
    staleTime: Infinity,
    enabled: playerId !== "",
  });
}

/**
 * Зміна мови: каталог перечитується сам (його ключ залежить від мови),
 * а чат — інвалідацією, бо переклади в історії прив'язані до мови читача.
 */
export function useChangeLanguage(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (language: string) =>
      api<void>(`/api/player/${playerId}/language`, { method: "POST", idempotent: true, body: { language } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.settings(playerId) });
      void queryClient.invalidateQueries({ queryKey: ["chat", playerId] });
    },
  });
}

/**
 * Вхід у гру (GDD §8.12): при відкритті й щойно за UTC настала нова доба —
 * гра могла простояти відкритою через північ. Нагороди приходять листами,
 * тож після входу перечитується скринька.
 */
export function useDailyCheckIn(playerId: string): void {
  const queryClient = useQueryClient();

  const { mutate } = useMutation({
    mutationFn: () => api<CheckInView>(`/api/player/${playerId}/check-in`, { method: "POST", idempotent: true }),
    onSuccess: (view) => {
      if (view.letters > 0) void queryClient.invalidateQueries({ queryKey: ["mail", playerId] });
    },
  });

  useEffect(() => {
    if (playerId === "") return;

    let checkedDay = "";

    const tick = () => {
      const day = new Date().toISOString().slice(0, 10);
      if (day === checkedDay) return;

      checkedDay = day;
      // Невдалий вхід (скажімо, гонка двох вкладок) повторимо на наступному тіку
      mutate(undefined, { onError: () => (checkedDay = "") });
    };

    tick();
    const timer = window.setInterval(tick, 60_000);

    return () => window.clearInterval(timer);
  }, [playerId, mutate]);
}
