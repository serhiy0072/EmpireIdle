import { useState } from "react";
import type { InventoryItemResponse } from "../../lib/apiTypes";
import { useMyClan } from "../../lib/queries/clans";
import { useGiftItem } from "../../lib/queries/inventory";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
  item: InventoryItemResponse;
}

/**
 * Подарунок соратнику (GDD §8.8). Список — лише свій клан: іншим сервер
 * однаково відмовить, тож і пропонувати їх не варто.
 */
export default function GiftPanel({ playerId, item }: Props) {
  const clan = useMyClan(playerId);
  const gift = useGiftItem(playerId);

  const [open, setOpen] = useState(false);
  const [recipientId, setRecipientId] = useState("");
  const [count, setCount] = useState(1);

  const clanmates = (clan.data?.members ?? []).filter((member) => member.playerId !== playerId);
  const safeCount = Math.min(item.count, Math.max(1, count));

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50"
      >
        Подарувати
      </button>
    );
  }

  return (
    <div className="mt-3 w-full space-y-2 rounded-lg bg-slate-50 p-2">
      <ErrorBanner error={gift.error} />

      {clan.data == null ? (
        <p className="text-sm text-slate-500">Дарувати можна лише членам свого клану — спершу вступіть у клан.</p>
      ) : clanmates.length === 0 ? (
        <p className="text-sm text-slate-500">У клані поки нікого, крім вас.</p>
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <select
            value={recipientId}
            onChange={(event) => setRecipientId(event.target.value)}
            className="min-w-0 flex-1 rounded-lg border border-slate-300 px-2 py-1 text-sm"
          >
            <option value="">Кому…</option>
            {clanmates.map((member) => (
              <option key={member.playerId} value={member.playerId}>
                {member.playerName}
              </option>
            ))}
          </select>

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
            disabled={recipientId === "" || gift.isPending}
            onClick={() =>
              gift.mutate(
                { recipientId, itemKey: item.itemKey, count: safeCount },
                { onSuccess: () => setOpen(false) },
              )
            }
            className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Подарувати
          </button>
        </div>
      )}

      <button type="button" onClick={() => setOpen(false)} className="text-xs text-slate-500 hover:underline">
        Скасувати
      </button>
    </div>
  );
}
