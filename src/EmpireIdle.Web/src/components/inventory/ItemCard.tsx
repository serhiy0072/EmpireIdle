import { useState } from "react";
import { Link } from "react-router-dom";
import type { InventoryItemResponse, UseItemRequest } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import GiftPanel from "./GiftPanel";
import ItemIcon from "./ItemIcon";

interface Props {
  playerId: string;
  item: InventoryItemResponse;
  busy: boolean;
  onUse: (request: UseItemRequest) => void;
}

const TYPE_LABELS: Record<string, string> = {
  resources: "Ресурси",
  boost: "Буст",
  teleport: "Телепорт",
  evolution: "Еволюція",
};

/**
 * Стаковий предмет. Ящики й бусти вживаються відразу, телепорт веде на мапу —
 * місце обирають там, есенції еволюції витрачає сам герой — їм кнопки не треба.
 */
export default function ItemCard({ playerId, item, busy, onUse }: Props) {
  const catalog = useCatalog();
  const [count, setCount] = useState(1);

  const giftable = catalog.item(item.itemKey)?.giftable === true;

  const usable = item.type === "resources" || item.type === "boost" || item.type === "scoutveil";
  const safeCount = Math.min(item.count, Math.max(1, count));

  const use = () => onUse({ itemKey: item.itemKey, count: safeCount, targetX: null, targetY: null });

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3">
      <div className="flex gap-3">
        <ItemIcon itemKey={item.itemKey} type={item.type} rarity={item.rarity} size={48} />

        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="truncate font-medium text-slate-800">{item.displayName}</span>
            <span className="text-sm text-slate-600">×{item.count}</span>
          </div>

          <div className="mt-1 flex items-center gap-1 text-xs">
            <span className={`rounded px-2 py-0.5 ${rarityStyle(item.rarity)}`}>{rarityLabel(item.rarity)}</span>
            <span className="text-slate-500">{TYPE_LABELS[item.type] ?? item.type}</span>
          </div>
        </div>
      </div>

      {item.description !== "" && <p className="mt-2 text-sm text-slate-600">{item.description}</p>}

      {usable && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {item.count > 1 && (
            <input
              type="number"
              min={1}
              max={item.count}
              value={safeCount}
              onChange={(event) => setCount(Number(event.target.value))}
              className="w-20 rounded-lg border border-slate-300 px-2 py-1 text-sm"
            />
          )}

          <button
            type="button"
            onClick={use}
            disabled={busy}
            className="ml-auto rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Використати
          </button>
        </div>
      )}

      {item.type === "teleport" && (
        <div className="mt-3 flex flex-wrap justify-end gap-2">
          {giftable && <GiftPanel playerId={playerId} item={item} />}
          <Link
            to={`/map?teleport=${encodeURIComponent(item.itemKey)}`}
            className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700"
          >
            Обрати місце на мапі →
          </Link>
        </div>
      )}

      {item.type === "evolution" && (
        <p className="mt-2 text-xs text-slate-500">Витрачається під час еволюції героя.</p>
      )}
    </div>
  );
}
