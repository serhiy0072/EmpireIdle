import { useState, type ReactNode } from "react";
import type { LootboxDrop } from "../../lib/apiTypes";
import { useCatalog, type Catalog } from "../../lib/queries/catalog";
import { rarityLabel } from "../../lib/rarity";
import ItemIcon from "../inventory/ItemIcon";
import { useHotkeys } from "../../hooks/useHotkeys";

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
    case "randomhero":
      return `Випадковий герой (${rarityLabel(key ?? "")})`;
    case "randomheroshards":
      return `Осколки випадкового героя (${rarityLabel(key ?? "")})`;
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
        {box.rolls > 1 && <span className="text-xs text-slate-500">{box.rolls} нагороди за відкриття</span>}
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

/**
 * Вміст скрині зброї: чиї шматки зброї вона дає. Шматки лягають на героя, якого гравець обирає
 * на його картці, тож тут лише перелік трійки й кількість за скриню.
 */
export function WeaponChestContents({ itemKey }: { itemKey: string }) {
  const catalog = useCatalog();
  const [open, setOpen] = useState(false);
  const chest = catalog.item(itemKey);

  if (chest === null || chest.weaponHeroes.length === 0) return null;

  return (
    <div className="mt-2 text-sm">
      <button type="button" onClick={() => setOpen(!open)} className="text-sky-700 hover:underline">
        {open ? "Сховати вміст" : "Вміст"}
      </button>

      {open && (
        <ul className="mt-2 divide-y divide-slate-100 rounded-lg border border-slate-200">
          {chest.weaponHeroes.map((heroKey) => (
            <li key={heroKey} className="flex items-center justify-between gap-2 px-2 py-1">
              <span className="text-slate-700">Зброя: {catalog.heroName(heroKey)}</span>
              <span className="tabular-nums text-slate-500">×{chest.weaponShards ?? 1} шматок на вибір</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Кнопка «ще раз» у вікні нагород: те саме відкриття ще раз, не виходячи з вікна. */
export interface DropsAgain {
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

interface DropsProps {
  drops: LootboxDrop[];
  onClose: () => void;
  again?: DropsAgain;
  /** Поки йде повторне відкриття — плитки приглушені, кнопка неактивна. */
  pending?: boolean;
}

/** Значок нагороди, що не є предметом рюкзака. */
const DROP_EMOJI: Record<string, string> = {
  gems: "💎",
  heroshards: "🧩",
  randomheroshards: "🧩",
  hero: "🦸",
  randomhero: "🦸",
  heroexperience: "📘",
};

const RESOURCE_EMOJI: Record<string, string> = { gold: "🪙", wood: "🪵", iron: "⛓️", food: "🍖" };

/** 1500 → «1,5K»: на плитці мало місця. */
function compact(amount: number): string {
  if (amount >= 1_000_000) return `${+(amount / 1_000_000).toFixed(1)}M`;
  if (amount >= 1_000) return `${+(amount / 1_000).toFixed(1)}K`;
  return String(amount);
}

function DropTile({ drop, amount, pity }: { drop: LootboxDrop; amount: number; pity: boolean }) {
  const catalog = useCatalog();
  const type = drop.type.toLowerCase();
  const key = drop.key ?? "";
  const item = type === "item" || type === "lootbox" ? catalog.item(key) : null;
  const name = rewardName(catalog, drop.type, drop.key);
  const rarity = item?.rarity ?? (type === "artifact" || type.startsWith("random") ? key : "Common");

  return (
    <div title={name} className="relative flex w-20 flex-col items-center gap-1">
      <div className={`relative rounded-xl ring-2 ${pity ? "ring-amber-400" : "ring-white/30"}`}>
        {item !== null || type === "artifact" ? (
          <ItemIcon itemKey={item?.key ?? `artifact_${key}`} type={item?.type ?? "artifact"} rarity={rarity} size={64} />
        ) : (
          <div className="flex h-16 w-16 items-center justify-center rounded-xl bg-slate-100 text-3xl">
            {type === "resource" ? (RESOURCE_EMOJI[key] ?? "📦") : (DROP_EMOJI[type] ?? "🎁")}
          </div>
        )}
        <span className="absolute -bottom-1 -right-1 rounded-md bg-slate-900/85 px-1.5 text-xs font-semibold tabular-nums text-white">
          {compact(amount)}
        </span>
        {pity && <span className="absolute -left-1 -top-1 text-sm">★</span>}
      </div>
      <span className="line-clamp-2 text-center text-[11px] leading-tight text-sky-50">{name}</span>
    </div>
  );
}

/**
 * Що випало — окремим вікном поверх екрана (як у референсі): розмір вікна й місце кнопки «ще раз»
 * не залежать від кількості нагород — зайве прокручується всередині сітки, тож кнопка не стрибає.
 * Однакові нагороди згорнуті в одну плитку з сумою. Клік будь-де поза кнопкою закриває вікно.
 */
export function LootboxDrops({ drops, onClose, again, pending = false }: DropsProps) {
  const merged = new Map<string, { drop: LootboxDrop; amount: number; pity: boolean }>();

  for (const drop of drops) {
    const id = `${drop.type}:${drop.key ?? ""}`;
    const seen = merged.get(id);

    if (seen === undefined) merged.set(id, { drop, amount: drop.amount, pity: drop.wasPity });
    else merged.set(id, { drop, amount: seen.amount + drop.amount, pity: seen.pity || drop.wasPity });
  }

  return (
    <RewardsWindow onClose={onClose} again={again} pending={pending}>
      {[...merged.entries()].map(([id, { drop, amount, pity }]) => (
        <DropTile key={id} drop={drop} amount={amount} pity={pity} />
      ))}
    </RewardsWindow>
  );
}

interface WindowProps {
  children: ReactNode;
  onClose: () => void;
  again?: DropsAgain;
  pending?: boolean;
}

/**
 * Рамка вікна нагород, спільна для скринь і банерів: сталий розмір, плитки прокручуються всередині,
 * кнопка «ще раз» не стрибає. Клік будь-де поза кнопкою чи Esc закриває вікно.
 */
export function RewardsWindow({ children, onClose, again, pending = false }: WindowProps) {
  useHotkeys({ Escape: onClose, Enter: onClose }, { modal: true });

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Нагороди"
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
    >
      <div className="flex h-[30rem] w-full max-w-md flex-col items-center rounded-2xl bg-gradient-to-b from-[#1d3f75] to-[#0f2347] p-4 shadow-2xl ring-1 ring-sky-300/40">
        <div className="w-full rounded-lg bg-gradient-to-r from-sky-400/30 via-sky-200/40 to-sky-400/30 py-2 text-center text-lg font-semibold tracking-wide text-white ring-1 ring-sky-200/60">
          Нагороди
        </div>

        <div className={`mt-4 min-h-0 w-full flex-1 overflow-y-auto ${pending ? "opacity-40" : ""}`}>
          <div className="flex flex-wrap content-start justify-center gap-3">
            {children}
          </div>
        </div>

        <div className="mt-3 text-5xl" aria-hidden>
          🧰
        </div>

        <div className="mt-3 flex h-10 items-center">
          {again !== undefined && (
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                again.onClick();
              }}
              disabled={pending || again.disabled}
              className="rounded-xl bg-gradient-to-b from-amber-300 to-amber-500 px-6 py-2 font-semibold text-amber-950 shadow ring-1 ring-amber-600 hover:from-amber-200 disabled:opacity-50"
            >
              {again.label}
            </button>
          )}
        </div>
        <p className="mt-2 text-xs text-sky-100/80">Щоб закрити, натисніть будь-де</p>
      </div>
    </div>
  );
}
