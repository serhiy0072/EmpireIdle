import type { ReactNode } from "react";
import { useNow } from "../../hooks/useNow";
import { useCatalog } from "../../lib/queries/catalog";
import type { MarketListingView } from "../../lib/queries/market";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import { statLabel } from "../../lib/statNames";
import { formatRemaining } from "../../lib/time";
import HeroPortrait from "../heroes/HeroPortrait";
import ItemIcon from "../inventory/ItemIcon";

interface Props {
  listing: MarketListingView;
  /** Кнопка дії: «Купити» на вітрині чи «Зняти» у своїх лотах. */
  action?: ReactNode;
}

const STATE_LABELS: Record<string, string> = {
  Sold: "Продано",
  Cancelled: "Знято",
  Expired: "Строк минув",
};

/** Лот ринку: товар, ціна за весь лот і за одиницю, скільки лишилось висіти. */
export default function ListingCard({ listing, action }: Props) {
  const catalog = useCatalog();
  const now = useNow();

  const hero = listing.kind === "Hero" ? catalog.hero(listing.itemKey) : null;
  const name = hero !== null ? catalog.heroName(listing.itemKey) : catalog.itemName(listing.itemKey);
  const rarity = listing.equipment?.rarity ?? hero?.rank ?? catalog.item(listing.itemKey)?.rarity;
  const unit = listing.kind === "Item" ? "шт." : "сили";

  return (
    <div className="flex gap-3 rounded-xl border border-slate-200 bg-white p-3">
      {listing.kind === "Hero" ? (
        <HeroPortrait heroKey={listing.itemKey} heroClass={hero?.class} rank={hero?.rank} tier={listing.hero?.tier ?? 1} size={48} />
      ) : (
        <ItemIcon itemKey={listing.itemKey} type={listing.kind === "Item" ? (catalog.item(listing.itemKey)?.type ?? "item") : "equipment"} rarity={rarity} size={48} />
      )}

      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <span className="truncate font-medium text-slate-800">
            {name}
            {listing.quantity > 1 && <span className="ml-1 text-slate-500">×{listing.quantity}</span>}
            {(listing.equipment?.enhancementLevel ?? 0) > 0 && (
              <span className="ml-1 text-amber-600">+{listing.equipment?.enhancementLevel}</span>
            )}
          </span>
          <span className="font-medium text-amber-700">{listing.priceGold.toLocaleString("uk-UA")} 🪙</span>
        </div>

        <div className="flex flex-wrap items-center gap-1 text-xs">
          {rarity !== undefined && <span className={`rounded px-2 py-0.5 ${rarityStyle(rarity)}`}>{rarityLabel(rarity)}</span>}
          {listing.hero != null && (
            <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">
              рів. {listing.hero.level} · тір {listing.hero.tier} · сузір'я {listing.hero.constellation}
            </span>
          )}
          {listing.equipment != null &&
            Object.entries(listing.equipment.stats).map(([stat, value]) => (
              <span key={stat} className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">
                {statLabel(stat)} {Math.round(value)}
              </span>
            ))}
        </div>

        <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
          <span>
            {listing.pricePerUnit.toLocaleString("uk-UA", { maximumFractionDigits: 1 })} 🪙 за одиницю {unit}
            {listing.state === "Active"
              ? ` · ще ${formatRemaining(listing.expiresAt, now)}`
              : ` · ${STATE_LABELS[listing.state] ?? listing.state}`}
          </span>
          {action}
        </div>
      </div>
    </div>
  );
}
