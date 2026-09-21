import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { WalletResponse } from "../apiTypes";
import { queryKeys } from "../queryKeys";

/** Баланс gems: акаунтний, той самий ключ інвалідує ServerQuestRewarded і покупки. */
export function useWallet(playerId: string): UseQueryResult<WalletResponse> {
  return useQuery({
    queryKey: queryKeys.wallet(playerId),
    queryFn: () => api<WalletResponse>(`/api/wallet/${playerId}`),
  });
}
