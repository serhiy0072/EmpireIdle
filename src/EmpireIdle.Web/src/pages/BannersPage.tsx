import { useState } from "react";
import BannerCard from "../components/banners/BannerCard";
import ErrorBanner from "../components/ErrorBanner";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { BannerRollResponse } from "../lib/apiTypes";
import { useBanners, useRollBanner } from "../lib/queries/banners";
import { useWallet } from "../lib/queries/wallet";
import { rarityLabel, rarityStyle } from "../lib/rarity";

export default function BannersPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();

  const banners = useBanners(playerId);
  const wallet = useWallet(playerId);
  const roll = useRollBanner(playerId);

  // Історія роллів цієї сесії: гравець бачить серію, а не лише останній випад
  const [history, setHistory] = useState<BannerRollResponse[]>([]);

  if (banners.isPending) {
    return <p className="text-slate-500">Завантаження банерів…</p>;
  }

  if (banners.isError) {
    return <ErrorBanner error={banners.error} />;
  }

  const gems = wallet.data?.gemBalance ?? 0;
  const latest = history[0] ?? null;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Банери</h1>
        <p className="text-sm text-slate-500">💎 {gems.toLocaleString("uk-UA")}</p>
      </div>

      <ErrorBanner error={roll.error} />

      {latest !== null && (
        <div
          role="status"
          className={`rounded-xl border px-4 py-3 ${
            latest.rarity === 3 ? "border-amber-300 bg-amber-50" : latest.rarity === 2 ? "border-sky-300 bg-sky-50" : "border-slate-200 bg-white"
          }`}
        >
          <div className="flex flex-wrap items-center gap-2">
            <span className={`rounded px-2 py-0.5 text-xs ${rarityStyle(latest.rarity)}`}>{rarityLabel(latest.rarity)}</span>
            <span className="text-lg font-medium text-slate-800">{latest.displayName}</span>
            {latest.wasPity && <span className="text-xs text-slate-500">гарантія</span>}
            {latest.lostFiftyFifty && <span className="text-xs text-slate-500">50/50 програно — наступний унікальний промо</span>}
          </div>
          {history.length > 1 && (
            <p className="mt-1 text-xs text-slate-500">
              Раніше: {history.slice(1, 6).map((entry) => entry.displayName).join(", ")}
            </p>
          )}
        </div>
      )}

      {banners.data.length === 0 ? (
        <p className="text-sm text-slate-500">Зараз немає активних банерів.</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {banners.data.map((banner) => (
            <BannerCard
              key={banner.key}
              banner={banner}
              gems={gems}
              now={now}
              busy={roll.isPending}
              onRoll={() =>
                roll.mutate(banner.key, {
                  onSuccess: (result) => setHistory((previous) => [result, ...previous].slice(0, 20)),
                })
              }
            />
          ))}
        </div>
      )}
    </div>
  );
}
