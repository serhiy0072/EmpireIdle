import { useState } from "react";
import BannerCard from "../components/banners/BannerCard";
import ErrorBanner from "../components/ErrorBanner";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { BannerDropResult } from "../lib/apiTypes";
import { MAX_ROLLS_PER_REQUEST, useBanners, useRollBanner } from "../lib/queries/banners";
import { useWallet } from "../lib/queries/wallet";
import { rarityLabel, rarityStyle } from "../lib/rarity";

/** Скільки випадів тримаємо в історії сесії. */
const HISTORY_LIMIT = 50;

function tone(rarity: number): string {
  return rarity === 3 ? "border-amber-300 bg-amber-50" : rarity === 2 ? "border-sky-300 bg-sky-50" : "border-slate-200 bg-white";
}

export default function BannersPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();

  const banners = useBanners(playerId);
  const wallet = useWallet(playerId);
  const roll = useRollBanner(playerId);

  // Остання серія — розгорнуто, попередні — рядком: гравець бачить і серію, і смугу
  const [latest, setLatest] = useState<BannerDropResult[]>([]);
  const [history, setHistory] = useState<BannerDropResult[]>([]);

  if (banners.isPending) {
    return <p className="text-slate-500">Завантаження банерів…</p>;
  }

  if (banners.isError) {
    return <ErrorBanner error={banners.error} />;
  }

  const gems = wallet.data?.gemBalance ?? 0;
  const seals = wallet.data?.sealBalance ?? 0;
  const best = latest.reduce((max, drop) => Math.max(max, drop.rarity), 0);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Банери</h1>
        <p className="text-sm text-slate-500">
          💎 {gems.toLocaleString("uk-UA")} · 🔮 {seals.toLocaleString("uk-UA")}
          <span className="ml-2 text-xs text-slate-400">печатки призову — за дублікати героїв понад стелю сузір'я</span>
        </p>
      </div>

      <ErrorBanner error={roll.error} />

      {latest.length > 0 && (
        <div role="status" className={`rounded-xl border px-4 py-3 ${tone(best)}`}>
          <ul className="grid gap-1 sm:grid-cols-2">
            {latest.map((drop, index) => (
              <li key={index} className="flex flex-wrap items-center gap-2 text-sm">
                <span className={`rounded px-2 py-0.5 text-xs ${rarityStyle(drop.rarity)}`}>{rarityLabel(drop.rarity)}</span>
                <span className={drop.rarity >= 2 ? "font-medium text-slate-800" : "text-slate-700"}>{drop.displayName}</span>
                {drop.wasPity && <span className="text-xs text-slate-500">гарантія</span>}
                {drop.lostFiftyFifty && <span className="text-xs text-slate-500">50/50 програно</span>}
              </li>
            ))}
          </ul>
          {history.length > 0 && (
            <p className="mt-2 text-xs text-slate-500">
              Раніше: {history.slice(0, 10).map((entry) => entry.displayName).join(", ")}
              {history.length > 10 && "…"}
            </p>
          )}
        </div>
      )}

      {banners.data.length === 0 ? (
        <p className="text-sm text-slate-500">Зараз немає активних банерів.</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {/* Постійний банер — першим: він завжди є, події приходять і йдуть */}
          {[...banners.data].sort((a, b) => Number(b.kind === 3) - Number(a.kind === 3)).map((banner) => (
            <BannerCard
              key={banner.key}
              banner={banner}
              gems={gems}
              seals={seals}
              now={now}
              busy={roll.isPending}
              maxRolls={MAX_ROLLS_PER_REQUEST}
              onRoll={(count, currency) =>
                roll.mutate(
                  { bannerKey: banner.key, count, currency },
                  {
                    onSuccess: (result) => {
                      setHistory((previous) => [...latest, ...previous].slice(0, HISTORY_LIMIT));
                      setLatest(result.drops);
                    },
                  },
                )
              }
            />
          ))}
        </div>
      )}
    </div>
  );
}
