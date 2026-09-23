import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { TutorialProgressResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

export function useTutorialProgress(playerId: string): UseQueryResult<TutorialProgressResponse> {
  return useQuery({
    queryKey: queryKeys.tutorial(playerId),
    queryFn: () => api<TutorialProgressResponse>(`/api/tutorial/${playerId}`),
    // Прогрес міняє лише цей клієнт: перепитувати на фокус вікна нема чого
    staleTime: Infinity,
  });
}

/**
 * Позначити крок побаченим. Оптимістично: картка зникає одразу, а не після
 * відповіді — інакше підказка "блимає" ще пів секунди після кліку.
 */
export function useMarkStepSeen(playerId: string) {
  const queryClient = useQueryClient();
  const key = queryKeys.tutorial(playerId);

  return useMutation({
    mutationFn: (stepKey: string) =>
      api<void>(`/api/tutorial/${playerId}/steps/${stepKey}`, { method: "POST", idempotent: true }),
    onMutate: async (stepKey) => {
      await queryClient.cancelQueries({ queryKey: key });

      queryClient.setQueryData<TutorialProgressResponse>(key, (current) => ({
        seenSteps: [...(current?.seenSteps ?? []), stepKey],
        skippedAt: current?.skippedAt ?? null,
      }));
    },
    onError: () => void queryClient.invalidateQueries({ queryKey: key }),
  });
}

export function useSkipTutorial(playerId: string) {
  const queryClient = useQueryClient();
  const key = queryKeys.tutorial(playerId);

  return useMutation({
    mutationFn: () => api<void>(`/api/tutorial/${playerId}/skip`, { method: "POST", idempotent: true }),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: key });

      queryClient.setQueryData<TutorialProgressResponse>(key, (current) => ({
        seenSteps: current?.seenSteps ?? [],
        skippedAt: new Date().toISOString(),
      }));
    },
    onError: () => void queryClient.invalidateQueries({ queryKey: key }),
  });
}
