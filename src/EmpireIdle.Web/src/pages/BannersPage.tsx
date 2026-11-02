import { useState } from "react";
import MarketTabs from "../components/shell/MarketTabs";
import BannerCard from "../components/banners/BannerCard";
import BannerDrops from "../components/banners/BannerDrops";
import ErrorBanner from "../components/ErrorBanner";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { BannerDropResult } from "../lib/apiTypes";
import { MAX_ROLLS_PER_REQUEST, useBanners, useRollBanner } from "../lib/queries/banners";
import { useWallet } from "../lib/queries/wallet";
import { rarityLabel, rarityStyle } from "../lib/rarity";
import { formatNumber } from "../lib/format";
import { t } from "i18next";

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
  // Відкрите вікно нагород і що крутили — для кнопки «Ще»
  const [shown, setShown] = useState<{ bannerKey: string; count: number } | null>(null);

  if (banners.isPending) {
    return <p className="text-slate-500">{t("items:banners.loading")}</p>;
  }

  if (banners.isError) {
    return <ErrorBanner error={banners.error} />;
  }

  const gems = wallet.data?.gemBalance ?? 0;
  const best = latest.reduce((max, drop) => Math.max(max, drop.rarity), 0);

  const rollBanner = (bannerKey: string, count: number) =>
    roll.mutate(
      { bannerKey, count },
      {
        onSuccess: (result) => {
          setHistory((previous) => [...latest, ...previous].slice(0, HISTORY_LIMIT));
          setLatest(result.drops);
          setShown({ bannerKey, count });
        },
      },
    );

  const shownBanner = shown === null ? undefined : banners.data.find((banner) => banner.key === shown.bannerKey);

  return (
    <div className="space-y-4">
      <MarketTabs />
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">{t("items:banners.title")}</h1>
        <p className="text-sm text-slate-500">
          💎 {formatNumber(gems)}
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
                {drop.wasPity && <span className="text-xs text-slate-500">{t("items:banners.pity")}</span>}
                {drop.lostFiftyFifty && <span className="text-xs text-slate-500">{t("items:banners.lostFiftyFifty")}</span>}
              </li>
            ))}
          </ul>
          {history.length > 0 && (
            <p className="mt-2 text-xs text-slate-500">
              {t("items:banners.history", { items: history.slice(0, 10).map((entry) => entry.displayName).join(", ") })}
              {history.length > 10 && "…"}
            </p>
          )}
        </div>
      )}

      {banners.data.length === 0 ? (
        <p className="text-sm text-slate-500">{t("items:banners.none")}</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {/* Постійний банер — першим: він завжди є, події приходять і йдуть */}
          {[...banners.data].sort((a, b) => Number(b.kind === 3) - Number(a.kind === 3)).map((banner) => (
            <BannerCard
              key={banner.key}
              banner={banner}
              gems={gems}
              now={now}
              busy={roll.isPending}
              maxRolls={MAX_ROLLS_PER_REQUEST}
              onRoll={(count) => rollBanner(banner.key, count)}
            />
          ))}
        </div>
      )}

      {shown !== null && shownBanner !== undefined && (
        <BannerDrops
          drops={latest}
          onClose={() => setShown(null)}
          pending={roll.isPending}
          again={{
            label: t("items:banners.again", { count: shown.count, price: formatNumber(shownBanner.priceGems * shown.count) }),
            onClick: () => rollBanner(shown.bannerKey, shown.count),
            disabled: gems < shownBanner.priceGems * shown.count,
          }}
        />
      )}
    </div>
  );
}
