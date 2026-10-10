import { useState } from "react";
import type { LootboxDrop } from "../../lib/apiTypes";
import { useCatalog, type Catalog } from "../../lib/queries/catalog";
import { rarityLabel } from "../../lib/rarity";

/** Назва позиції скрині чи того, що з неї випало, — за типом нагороди. */
function rewardName(catalog: Catalog, type: string, key: string | null | undefined): string {
  switch (type.toLowerCase()) {
    case "gems":
      return "Самоцвіти 💎";
    case "resource":
      return catalog.resourceName(key ?? "");
    case "heroshards":
      return `Осколки: ${catalog.heroName(key ?? "")}`;
    case "hero":
      return `Герой: ${catalog.heroName(key ?? "")}`;
    case "heroexperience":
      return "Досвід героїв";
    case "artifact":
      return `Випадковий артефакт (${rarityLabel(key ?? "")})`;
    default:
      return catalog.itemName(key ?? "");
  }
}

interface OddsProps {
  itemKey: string;
  /** Відкриттів поспіль без позиції гарантії; undefined — лічильник ще не приїхав. */
  since?: number;
}

/**
 * Шанси скрині (GDD §9.12): розкриття обов'язкове, тож показуємо рівно ту таблицю, з якою кидає сервер.
 * Позиції гарантії позначені, а над таблицею — скільки до неї лишилось.
 */
export function LootboxOdds({ itemKey, since }: OddsProps) {
  const catalog = useCatalog();
  const [open, setOpen] = useState(false);
  const box = catalog.lootbox(itemKey);

  if (box === null) return null;

  const left = box.pityWithin > 0 && since !== undefined ? box.pityWithin - since : null;

  return (
    <div className="mt-2 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <button type="button" onClick={() => setOpen(!open)} className="text-sky-700 hover:underline">
          {open ? "Сховати шанси" : "Шанси"}
        </button>
        {box.pityWithin > 0 && (
          <span className="rounded bg-amber-100 px-2 py-0.5 text-xs text-amber-800">
            {left === null ? `гарантія — не пізніше ${box.pityWithin}-ї` : `гарантія через ${left}`}
          </span>
        )}
      </div>

      {open && (
        <ul className="mt-2 divide-y divide-slate-100 rounded-lg border border-slate-200">
          {box.entries.map((entry, index) => (
            <li key={index} className="flex items-center justify-between gap-2 px-2 py-1">
              <span className="text-slate-700">
                {rewardName(catalog, entry.type, entry.key)} ×{entry.amount.toLocaleString("uk-UA")}
                {entry.pity && <span className="ml-1 text-xs text-amber-700">★ гарантія</span>}
              </span>
              <span className="tabular-nums text-slate-500">{entry.chance}%</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

interface DropsProps {
  drops: LootboxDrop[];
  onClose: () => void;
}

/** Що випало: однакові рядки згорнуті в один з сумою. */
export function LootboxDrops({ drops, onClose }: DropsProps) {
  const catalog = useCatalog();

  const merged = new Map<string, { drop: LootboxDrop; amount: number; pity: boolean }>();

  for (const drop of drops) {
    const id = `${drop.type}:${drop.key ?? ""}`;
    const seen = merged.get(id);

    if (seen === undefined) merged.set(id, { drop, amount: drop.amount, pity: drop.wasPity });
    else merged.set(id, { drop, amount: seen.amount + drop.amount, pity: seen.pity || drop.wasPity });
  }

  return (
    <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-3">
      <div className="flex items-baseline justify-between">
        <span className="font-medium text-emerald-900">Випало</span>
        <button type="button" onClick={onClose} className="text-sm text-emerald-800 hover:underline">
          закрити
        </button>
      </div>
      <ul className="mt-2 space-y-1 text-sm">
        {[...merged.entries()].map(([id, { drop, amount, pity }]) => (
          <li key={id} className="flex justify-between gap-2 text-slate-800">
            <span>
              {rewardName(catalog, drop.type, drop.key)}
              {pity && <span className="ml-1 text-xs text-amber-700">★ гарантія</span>}
            </span>
            <span className="tabular-nums">×{amount.toLocaleString("uk-UA")}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
