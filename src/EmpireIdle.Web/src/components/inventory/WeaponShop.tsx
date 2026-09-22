import { useCatalog } from "../../lib/queries/catalog";
import { rarityLabel, rarityStyle } from "../../lib/rarity";

interface Props {
  busy: boolean;
  onBuy: (itemKey: string) => void;
}

/** Зброя з каталогу за золото. Артефакти сюди не потрапляють — вони лише з банерів і лутбоксів. */
export default function WeaponShop({ busy, onBuy }: Props) {
  const catalog = useCatalog();
  const weapons = catalog.weaponsForSale;

  if (weapons.length === 0) {
    return <p className="text-sm text-slate-500">Кузня поки нічого не продає.</p>;
  }

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {weapons.map((weapon) => (
        <div key={weapon.key} className="rounded-xl border border-slate-200 bg-white p-3">
          <div className="flex items-baseline justify-between gap-2">
            <span className="font-medium text-slate-800">{weapon.displayName}</span>
            <span className={`rounded px-2 py-0.5 text-xs ${rarityStyle(weapon.rarity)}`}>{rarityLabel(weapon.rarity)}</span>
          </div>

          <p className="mt-1 text-xs text-slate-500">
            {Object.entries(weapon.baseStats)
              .map(([stat, value]) => `${stat} ${value}`)
              .join(" · ")}
            {weapon.weaponClasses.length > 0 && ` · для: ${weapon.weaponClasses.join(", ")}`}
          </p>

          <div className="mt-3 flex items-center justify-between">
            <span className="text-sm text-slate-600">{weapon.priceGold.toLocaleString("uk-UA")} золота</span>
            <button
              type="button"
              onClick={() => onBuy(weapon.key)}
              disabled={busy}
              className="rounded-lg bg-amber-500 px-3 py-1 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
            >
              Купити
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
