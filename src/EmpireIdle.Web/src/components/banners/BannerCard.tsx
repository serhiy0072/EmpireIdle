import type { BannerView } from "../../lib/apiTypes";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import { formatRemaining } from "../../lib/time";

interface Props {
  banner: BannerView;
  gems: number;
  now: number;
  busy: boolean;
  onRoll: () => void;
}

/**
 * Банер із лічильниками гарантій. Pity спільний на групу, тож
 * "з останнього рідкісного" однаковий у банерів однієї групи.
 */
export default function BannerCard({ banner, gems, now, busy, onRoll }: Props) {
  const isHero = banner.kind === 1;
  const rareLeft = Math.max(0, banner.rarePity - banner.rareSince);
  const uniqueLeft = Math.max(0, banner.uniquePity - banner.uniqueSince);
  const featured = banner.featuredKey === null || banner.featuredKey === undefined
    ? null
    : (banner.drops.find((drop) => drop.key === banner.featuredKey) ?? null);
  const canPay = gems >= banner.priceGems;

  return (
    <div className={`rounded-xl border bg-white p-4 ${isHero ? "border-violet-200" : "border-amber-200"}`}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <div>
          <h2 className="font-medium text-slate-800">{banner.displayName}</h2>
          <p className="text-xs text-slate-500">
            {isHero ? "Герої" : "Зброя"}
            {banner.endsAt !== null && banner.endsAt !== undefined && ` · до кінця ${formatRemaining(banner.endsAt, now)}`}
          </p>
        </div>
        <span className="rounded-full bg-violet-100 px-3 py-1 text-sm text-violet-800">💎 {banner.priceGems}</span>
      </div>

      {featured !== null && (
        <div className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-900">
          Промо: <span className="font-medium">{featured.displayName}</span>
          {banner.featuredGuaranteed && " — наступний унікальний гарантовано промо"}
        </div>
      )}

      <div className="mt-3 grid grid-cols-2 gap-2 text-sm">
        <div className="rounded-lg bg-slate-50 px-3 py-2">
          <div className="text-xs text-slate-500">Рідкісний гарантовано через</div>
          <div className="font-medium text-slate-800">
            {rareLeft} <span className="text-xs text-slate-500">/ {banner.rarePity}</span>
          </div>
        </div>
        <div className="rounded-lg bg-slate-50 px-3 py-2">
          <div className="text-xs text-slate-500">Унікальний гарантовано через</div>
          <div className="font-medium text-slate-800">
            {uniqueLeft} <span className="text-xs text-slate-500">/ {banner.uniquePity}</span>
          </div>
        </div>
      </div>

      <details className="mt-3 text-sm">
        <summary className="cursor-pointer text-slate-600">Шанси · {banner.drops.length} нагород</summary>
        <ul className="mt-2 space-y-1">
          {banner.drops.map((drop) => (
            <li key={drop.key} className="flex items-center justify-between gap-2">
              <span className="flex items-center gap-2">
                <span className={`rounded px-2 py-0.5 text-xs ${rarityStyle(drop.rarity)}`}>{rarityLabel(drop.rarity)}</span>
                <span className="text-slate-700">{drop.displayName}</span>
              </span>
              <span className="text-xs text-slate-500">{drop.chance.toFixed(2)}%</span>
            </li>
          ))}
        </ul>
      </details>

      <button
        type="button"
        onClick={onRoll}
        disabled={busy || !canPay}
        className="mt-4 w-full rounded-lg bg-violet-600 px-3 py-2 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50"
      >
        {canPay ? `Крутити за ${banner.priceGems} 💎` : "Не вистачає самоцвітів"}
      </button>
    </div>
  );
}
