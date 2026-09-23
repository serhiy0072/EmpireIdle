import { useMutation, useQuery, useQueryClient, type UseQueryResult } from "@tanstack/react-query";
import { api } from "../api";
import type { BannerRollResponse, BannerView } from "../apiTypes";
import { queryKeys } from "../queryKeys";
import { invalidatePlayer } from "./invalidate";
import { refetchAtDue } from "./polling";

export function useBanners(playerId: string): UseQueryResult<BannerView[]> {
  return useQuery({
    queryKey: queryKeys.banners(playerId),
    queryFn: () => api<BannerView[]>(`/api/banners/${playerId}`),
    // Подієвий банер зникає зі списку в endsAt — перепитуємо саме тоді
    refetchInterval: refetchAtDue<BannerView[]>((banners) => banners.map((banner) => banner.endsAt)),
  });
}

/** Стеля серії за один запит — та сама, що й у RollBannerCommand.MaxCount на сервері. */
export const MAX_ROLLS_PER_REQUEST = 10;

/**
 * Серія роллів одним запитом: списує gems і кладе героїв чи зброю в інвентар.
 * Лічильники гарантій приходять у відповіді, але список банерів усе одно
 * перечитуємо: pity спільний на групу, тож змінилися й сусідні банери.
 */
/** Валюта ролла: числа з контракту BannerCurrency — сервер приймає enum числом. */
export type BannerCurrency = "gems" | "seals";

const CURRENCY_CODE: Record<BannerCurrency, number> = { gems: 0, seals: 1 };

export function useRollBanner(playerId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (input: { bannerKey: string; count: number; currency: BannerCurrency }) =>
      api<BannerRollResponse>(
        `/api/banners/${playerId}/${input.bannerKey}/roll?count=${input.count}&currency=${CURRENCY_CODE[input.currency]}`,
        { method: "POST", idempotent: true },
      ),
    onSuccess: () => invalidatePlayer(queryClient, playerId, ["banners", "wallet", "heroes", "inventory"]),
  });
}
