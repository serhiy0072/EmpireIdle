import { useState } from "react";
import type { InventoryItemResponse, UseItemRequest } from "../../lib/apiTypes";
import { rarityLabel, rarityStyle } from "../../lib/rarity";

interface Props {
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
 * Стаковий предмет. Ящики й бусти вживаються відразу, телепорт просить
 * координати, есенції еволюції витрачає сам герой — їм кнопки не треба.
 */
export default function ItemCard({ item, busy, onUse }: Props) {
  const [count, setCount] = useState(1);
  const [target, setTarget] = useState({ x: 0, y: 0 });

  const usable = item.type === "resources" || item.type === "boost" || item.type === "teleport";
  const safeCount = Math.min(item.count, Math.max(1, count));

  const use = () =>
    onUse({
      itemKey: item.itemKey,
      count: item.type === "teleport" ? 1 : safeCount,
      targetX: item.type === "teleport" ? target.x : null,
      targetY: item.type === "teleport" ? target.y : null,
    });

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3">
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-medium text-slate-800">{item.displayName}</span>
        <span className="text-sm text-slate-600">×{item.count}</span>
      </div>

      <div className="mt-1 flex items-center gap-1 text-xs">
        <span className={`rounded px-2 py-0.5 ${rarityStyle(item.rarity)}`}>{rarityLabel(item.rarity)}</span>
        <span className="text-slate-500">{TYPE_LABELS[item.type] ?? item.type}</span>
      </div>

      {item.description !== "" && <p className="mt-2 text-sm text-slate-600">{item.description}</p>}

      {usable && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          {item.type === "teleport" ? (
            <>
              <label className="text-xs text-slate-500">
                X
                <input
                  type="number"
                  value={target.x}
                  onChange={(event) => setTarget((previous) => ({ ...previous, x: Number(event.target.value) }))}
                  className="ml-1 w-16 rounded-lg border border-slate-300 px-2 py-1 text-sm text-slate-800"
                />
              </label>
              <label className="text-xs text-slate-500">
                Y
                <input
                  type="number"
                  value={target.y}
                  onChange={(event) => setTarget((previous) => ({ ...previous, y: Number(event.target.value) }))}
                  className="ml-1 w-16 rounded-lg border border-slate-300 px-2 py-1 text-sm text-slate-800"
                />
              </label>
            </>
          ) : (
            item.count > 1 && (
              <input
                type="number"
                min={1}
                max={item.count}
                value={safeCount}
                onChange={(event) => setCount(Number(event.target.value))}
                className="w-20 rounded-lg border border-slate-300 px-2 py-1 text-sm"
              />
            )
          )}

          <button
            type="button"
            onClick={use}
            disabled={busy}
            className="ml-auto rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {item.type === "teleport" ? "Переселитися" : "Використати"}
          </button>
        </div>
      )}

      {item.type === "evolution" && (
        <p className="mt-2 text-xs text-slate-500">Витрачається під час еволюції героя.</p>
      )}
    </div>
  );
}
