import { useState } from "react";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { useCatalog, type SpeedUpTimer } from "../lib/queries/catalog";
import { useInventory, useSpeedUpWithItems } from "../lib/queries/inventory";
import { speedUpMinutesLabel, suggestSpeedUps, type SpeedUpStock } from "../lib/speedUp";
import ErrorBanner from "./ErrorBanner";
import ItemIcon from "./inventory/ItemIcon";

interface Props {
  timer: SpeedUpTimer;
  targetId: string;
  completesAt: string;
}

/**
 * Прискорення предметами поруч із gems (рішення 08.10.2026). Кнопки немає, доки в рюкзаку
 * немає жодного прискорення: тоді гравцеві лишається лише ціна в gems.
 */
export default function SpeedUpItemsButton({ timer, targetId, completesAt }: Props) {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const now = useNow();
  const inventory = useInventory(playerId);
  const speedUp = useSpeedUpWithItems(playerId);
  const [open, setOpen] = useState(false);
  const [picked, setPicked] = useState<Record<string, number>>({});

  const stock: SpeedUpStock[] = (inventory.data?.items ?? [])
    .filter((item) => item.type === "speedup")
    .map((item) => ({ itemKey: item.itemKey, rarity: item.rarity, minutes: catalog.item(item.itemKey)?.speedUpMinutes ?? 0, count: item.count }))
    .filter((item) => item.minutes > 0)
    .sort((a, b) => a.minutes - b.minutes);

  const minutesLeft = Math.max(0, (new Date(completesAt).getTime() - now) / 60_000 - catalog.speedUpFloorSeconds(timer) / 60);

  if (stock.length === 0 || minutesLeft <= 0) return null;

  const total = stock.reduce((sum, item) => sum + (picked[item.itemKey] ?? 0) * item.minutes, 0);
  const change = (itemKey: string, delta: number, max: number) =>
    setPicked((current) => ({ ...current, [itemKey]: Math.min(max, Math.max(0, (current[itemKey] ?? 0) + delta)) }));

  const apply = () => {
    const items = Object.fromEntries(Object.entries(picked).filter(([, count]) => count > 0));
    speedUp.mutate(
      { timer, targetId, items },
      {
        onSuccess: () => {
          setOpen(false);
          setPicked({});
        },
      },
    );
  };

  return (
    <span className="relative inline-block">
      <button
        type="button"
        onClick={() => {
          setOpen(!open);
          if (!open) setPicked(suggestSpeedUps(stock, Math.ceil(minutesLeft)));
        }}
        title="Прискорити предметами"
        className="rounded-lg border border-sky-300 bg-sky-50 px-2 py-0.5 text-xs text-sky-800 hover:bg-sky-100"
      >
        ⏩ предмети
      </button>

      {open && (
        <div className="absolute right-0 z-20 mt-1 w-72 space-y-2 rounded-xl bg-[#1d3b6f] p-3 text-left text-white shadow-xl ring-1 ring-[#335c9a]">
          <p className="text-xs text-sky-100/80">Лишилось ≈ {Math.ceil(minutesLeft)} хв. Надлишок хвилин згорає.</p>
          <ul className="space-y-1">
            {stock.map((item) => (
              <li key={item.itemKey} className="flex items-center gap-2 text-sm">
                <ItemIcon itemKey={item.itemKey} type="speedup" rarity={item.rarity} size={28} />
                <span className="flex-1">
                  {speedUpMinutesLabel(item.minutes)} <span className="text-xs text-sky-200/70">(є {item.count})</span>
                </span>
                <button type="button" onClick={() => change(item.itemKey, -1, item.count)} className="h-6 w-6 rounded bg-slate-700">
                  −
                </button>
                <span className="w-6 text-center font-semibold">{picked[item.itemKey] ?? 0}</span>
                <button type="button" onClick={() => change(item.itemKey, 1, item.count)} className="h-6 w-6 rounded bg-slate-700">
                  +
                </button>
              </li>
            ))}
          </ul>
          <div className="flex items-center justify-between gap-2 text-sm">
            <span>
              Разом: <span className={total >= minutesLeft ? "font-bold text-emerald-300" : "font-bold"}>{total} хв</span>
            </span>
            <button
              type="button"
              onClick={() => setPicked(suggestSpeedUps(stock, Math.ceil(minutesLeft)))}
              className="text-xs text-sky-200 underline"
            >
              підібрати
            </button>
          </div>
          <button
            type="button"
            onClick={apply}
            disabled={speedUp.isPending || total === 0}
            className="w-full rounded-lg bg-gradient-to-b from-sky-400 to-blue-600 py-1.5 text-sm font-semibold disabled:opacity-50"
          >
            Використати
          </button>
          <ErrorBanner error={speedUp.error} />
        </div>
      )}
    </span>
  );
}
