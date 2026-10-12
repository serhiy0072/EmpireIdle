import { useState } from "react";
import { useNow } from "../../hooks/useNow";
import type { KeyChestView, LootboxDrop } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { useKeyChests, useLootboxPity, useOpenKeyChest } from "../../lib/queries/lootboxes";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";
import { LootboxDrops, LootboxOdds } from "./LootboxPanels";

/** Скільки скринь відкрити ключами за раз — не більше, ніж ключів, і не більше за ліміт запиту. */
const KEY_BATCH = 10;

interface CardProps {
  chest: KeyChestView;
  since: number | undefined;
  busy: boolean;
  onOpen: (free: boolean, count: number) => void;
}

/** Одна ключова скриня: безкоштовні відкриття з таймером і відкриття ключами. */
function KeyChestCard({ chest, since, busy, onOpen }: CardProps) {
  const catalog = useCatalog();
  const now = useNow();

  // Сервер віддає момент наступного безкоштовного; минув — відкрити можна вже, без перезапиту
  const ready = chest.freeLeftToday > 0 && (chest.nextFreeAt == null || Date.parse(chest.nextFreeAt) <= now);
  const batch = Math.min(chest.keys, KEY_BATCH);

  return (
    <div id={chest.chestKey} className="rounded-xl border border-amber-300 bg-gradient-to-b from-amber-50 to-white p-3">
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-medium text-slate-800">{catalog.itemName(chest.chestKey)}</span>
        <span className="text-xs text-slate-500">
          безкоштовно {chest.freeLeftToday} / {chest.freePerDay} · 🗝 {chest.keys.toLocaleString("uk-UA")}
        </span>
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => onOpen(true, 1)}
          disabled={busy || !ready}
          className="rounded-lg bg-amber-500 px-3 py-1 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
        >
          {ready
            ? "Відкрити безкоштовно"
            : chest.nextFreeAt != null
              ? `Безкоштовно через ${formatRemaining(chest.nextFreeAt, now)}`
              : "Безкоштовних на сьогодні немає"}
        </button>
        <button
          type="button"
          onClick={() => onOpen(false, 1)}
          disabled={busy || chest.keys < 1}
          className="rounded-lg border border-amber-400 px-3 py-1 text-sm font-medium text-amber-800 hover:bg-amber-50 disabled:opacity-50"
        >
          Ключем
        </button>
        {batch > 1 && (
          <button
            type="button"
            onClick={() => onOpen(false, batch)}
            disabled={busy}
            className="rounded-lg border border-amber-400 px-3 py-1 text-sm font-medium text-amber-800 hover:bg-amber-50 disabled:opacity-50"
          >
            Ключами ×{batch}
          </button>
        )}
        <a href={`#${chest.keyItemKey}`} className="text-sm text-sky-700 hover:underline">
          {catalog.itemName(chest.keyItemKey)} — у крамниці ↓
        </a>
      </div>

      <LootboxOdds itemKey={chest.chestKey} since={since} />
    </div>
  );
}

/**
 * Ключові скрині (рішення 12.10.2026): кілька безкоштовних відкриттів на добу з перезарядкою,
 * понад них — ключі з крамниці. Кожне відкриття — чотири нагороди.
 */
export default function KeyChests({ playerId }: { playerId: string }) {
  const chests = useKeyChests(playerId);
  const pity = useLootboxPity(playerId);
  const open = useOpenKeyChest(playerId);
  const [drops, setDrops] = useState<LootboxDrop[] | null>(null);

  if (chests.data === undefined || chests.data.length === 0) return null;

  const onOpen = (chestKey: string) => (free: boolean, count: number) =>
    open.mutate({ chestKey, free, count }, { onSuccess: (opening) => setDrops(opening.drops) });

  return (
    <section className="space-y-2">
      <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Скрині з ключами</h2>

      <ErrorBanner error={open.error} />
      {drops !== null && <LootboxDrops drops={drops} onClose={() => setDrops(null)} />}

      <div className="grid gap-3 sm:grid-cols-2">
        {chests.data.map((chest) => (
          <KeyChestCard
            key={chest.chestKey}
            chest={chest}
            since={pity.data?.find((row) => row.lootboxKey === chest.chestKey)?.since}
            busy={open.isPending}
            onOpen={onOpen(chest.chestKey)}
          />
        ))}
      </div>
    </section>
  );
}
