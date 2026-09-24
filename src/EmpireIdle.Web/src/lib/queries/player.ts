import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { components } from "../schema";
import { queryKeys } from "../queryKeys";

export type PlayerSettingsView = components["schemas"]["PlayerSettingsView"];

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
