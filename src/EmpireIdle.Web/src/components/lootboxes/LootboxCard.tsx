import { useState } from "react";
import ErrorBanner from "../ErrorBanner";
import ItemIcon from "../inventory/ItemIcon";
import type { InventoryItemResponse, LootboxDrop } from "../../lib/apiTypes";
import { useLootboxPity, useOpenLootbox } from "../../lib/queries/lootboxes";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import { LootboxOdds } from "./LootboxPanels";

/** Сервер відкриває щонайбільше стільки скринь за запит (OpenLootboxCommand.MaxCount). */
const MAX_PER_OPEN = 10;

interface Props {
  playerId: string;
  item: InventoryItemResponse;
  /** Що випало — показує сторінка: остання відкрита скриня зникає з рюкзака разом із цією карткою. */
  onOpened: (drops: LootboxDrop[]) => void;
}

/** Скриня в рюкзаку (GDD §9.12): шанси, гарантія, відкриття й те, що випало. */
export default function LootboxCard({ playerId, item, onOpened }: Props) {
  const open = useOpenLootbox(playerId);
  const pity = useLootboxPity(playerId);
  const [count, setCount] = useState(1);

  const limit = Math.min(item.count, MAX_PER_OPEN);
  const safeCount = Math.min(limit, Math.max(1, count));

  return (
    <div className="space-y-2">
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
              <span className="text-slate-500">Скриня</span>
            </div>
          </div>
        </div>

        {item.description !== "" && <p className="mt-2 text-sm text-slate-600">{item.description}</p>}

        <LootboxOdds itemKey={item.itemKey} since={pity.data?.find((p) => p.lootboxKey === item.itemKey)?.since} />

        <div className="mt-3 flex items-center gap-2">
          {limit > 1 && (
            <input
              type="number"
              min={1}
              max={limit}
              value={safeCount}
              onChange={(event) => setCount(Number(event.target.value))}
              className="w-16 rounded-lg border border-slate-300 px-2 py-1 text-sm"
            />
          )}
          <button
            type="button"
            onClick={() =>
              open.mutate({ itemKey: item.itemKey, count: safeCount }, { onSuccess: (result) => onOpened(result.drops) })
            }
            disabled={open.isPending}
            className="ml-auto rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Відкрити{safeCount > 1 ? ` ${safeCount}` : ""}
          </button>
        </div>
      </div>

      <ErrorBanner error={open.error} />
    </div>
  );
}
