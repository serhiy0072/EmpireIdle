import type { BannerView } from "../../lib/apiTypes";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import { formatRemaining } from "../../lib/time";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";
import { Trans } from "react-i18next";

interface Props {
  banner: BannerView;
  gems: number;
  now: number;
  busy: boolean;
  /** Стеля серії за один запит. */
  maxRolls: number;
  onRoll: (count: number) => void;
}

/** Kind з контракту: герої (подія) або стандартний (постійний, без промо). */
function kindLabel(kind: BannerView["kind"]): string {
  return t(`items:banner.kind.${kind}`);
}

/**
 * Банер із лічильниками гарантій. Pity спільний на групу, тож
 * "з останнього рідкісного" однаковий у банерів однієї групи.
 */
export default function BannerCard({ banner, gems, now, busy, maxRolls, onRoll }: Props) {
  const isHero = banner.kind === "Hero";
  const isStandard = banner.kind === "Standard";
  const unit = banner.priceGems;
  const balance = gems;
  const icon = "💎";
  const rareLeft = Math.max(0, banner.rarePity - banner.rareSince);
  const uniqueLeft = Math.max(0, banner.uniquePity - banner.uniqueSince);
  const featured = banner.featuredKey === null || banner.featuredKey === undefined
    ? null
    : (banner.drops.find((drop) => drop.key === banner.featuredKey) ?? null);
  const canPay = balance >= unit;
  const canPaySeries = balance >= unit * maxRolls;

  return (
    <div className={`rounded-xl border bg-[#1d3b6f] p-4 ${isStandard ? "border-[#4a72b0]" : isHero ? "border-violet-400/50" : "border-amber-400/50"}`}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <div>
          <h2 className="font-medium text-white">{banner.displayName}</h2>
          <p className="text-xs text-sky-100/70">
            {kindLabel(banner.kind)}
            {banner.endsAt !== null && banner.endsAt !== undefined && ` · ${t("items:banner.endsIn", { remaining: formatRemaining(banner.endsAt, now) })}`}
          </p>
        </div>
        <div className="flex gap-1 text-sm">
          <span className="rounded-full bg-violet-500/25 px-3 py-1 text-violet-200">💎 {banner.priceGems}</span>
        </div>
      </div>

      {featured !== null && (
        <div className="mt-3 rounded-lg bg-amber-500/15 px-3 py-2 text-sm text-amber-200">
          <Trans ns="items" i18nKey="banner.featured" values={{ name: featured.displayName }} components={{ b: <span className="font-medium" /> }} />
          {banner.featuredGuaranteed && ` — ${t("items:banner.featuredGuaranteed")}`}
        </div>
      )}

      <div className="mt-3 grid grid-cols-2 gap-2 text-sm">
        <div className="rounded-lg bg-white/5 px-3 py-2">
          <div className="text-xs text-sky-100/70">{t("items:banner.rarePity")}</div>
          <div className="font-medium text-white">
            {rareLeft} <span className="text-xs text-sky-100/70">/ {banner.rarePity}</span>
          </div>
        </div>
        <div className="rounded-lg bg-white/5 px-3 py-2">
          <div className="text-xs text-sky-100/70">{t("items:banner.uniquePity")}</div>
          <div className="font-medium text-white">
            {uniqueLeft} <span className="text-xs text-sky-100/70">/ {banner.uniquePity}</span>
          </div>
        </div>
      </div>

      <details className="mt-3 text-sm">
        <summary className="cursor-pointer text-slate-200">{t("items:banner.odds", { count: banner.drops.length })}</summary>
        <ul className="mt-2 space-y-1">
          {banner.drops.map((drop) => (
            <li key={drop.key} className="flex items-center justify-between gap-2">
              <span className="flex items-center gap-2">
                <span className={`rounded px-2 py-0.5 text-xs ${rarityStyle(drop.rarity)}`}>{rarityLabel(drop.rarity)}</span>
                <span className="text-slate-100">{drop.displayName}</span>
              </span>
              <span className="text-xs text-sky-100/70">{drop.chance.toFixed(3)}%</span>
            </li>
          ))}
        </ul>
      </details>

      <div className="mt-3 grid grid-cols-2 gap-2">
        <button
          type="button"
          onClick={() => onRoll(1)}
          disabled={busy || !canPay}
          className="rounded-lg border border-violet-400/50 px-3 py-2 text-sm font-medium text-violet-200 hover:bg-violet-500/15 disabled:opacity-50"
        >
          {canPay ? `×1 · ${unit} ${icon}` : t("items:banner.notEnough", { icon })}
        </button>
        {/* Серія — один запит: гравець не впирається в ліміт запитів, а гарантія рахується всередині серії */}
        <button
          type="button"
          onClick={() => onRoll(maxRolls)}
          disabled={busy || !canPaySeries}
          className="rounded-lg bg-violet-600 px-3 py-2 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50"
        >
          ×{maxRolls} · {formatNumber(unit * maxRolls)} {icon}
        </button>
      </div>
    </div>
  );
}
