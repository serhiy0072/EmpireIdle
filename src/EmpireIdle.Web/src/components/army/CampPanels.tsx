import { useEffect, useRef, useState } from "react";
import { useNow } from "../../hooks/useNow";
import { cumulativeUnitLevelCost, unitStatMultiplier } from "../../lib/progression";
import type { CatalogUnit, SpeedUpTimer } from "../../lib/queries/catalog";
import { useCatalog } from "../../lib/queries/catalog";
import { formatDuration, formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";
import { GameButton } from "../game/GameUi";
import SpeedUpItemsButton from "../SpeedUpItemsButton";
import { roman, tierFrame } from "../../lib/unitTiers";
import { UnitFigure } from "./UnitArt";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";

/** Шестикутник із загостреним верхом — рамка юніта, як на референсі табору. */
const HEX = "polygon(50% 0, 100% 25%, 100% 75%, 50% 100%, 0 75%, 0 25%)";

/** Мініатюра юніта в шестикутній рамці кольору рівня. */
export function UnitHex({ unitType, level, size, dimmed }: { unitType: string; level: number; size: number; dimmed?: boolean }) {
  const frame = tierFrame(level);

  return (
    <span
      className={`relative flex items-end justify-center overflow-hidden ${dimmed === true ? "grayscale" : ""}`}
      style={{ width: size, height: size * 1.1, clipPath: HEX, background: `linear-gradient(${frame.from}, ${frame.to})` }}
    >
      <span className="absolute inset-[3px] bg-gradient-to-b from-white/30 to-slate-900/30" style={{ clipPath: HEX }} />
      <span className="relative mb-2">
        <UnitFigure unitType={unitType} level={level} size={size * 0.62} />
      </span>
      <span className="absolute bottom-0.5 rounded-full bg-slate-900/70 px-1.5 text-[10px] font-bold leading-4 text-white">
        {roman(level)}
      </span>
    </span>
  );
}

interface StripProps {
  unitType: string;
  maxLevel: number;
  /** Вище цієї межі тренувати й качати ще не можна — плитка сіра (не вище ратуші). */
  ceiling: number;
  counts: Record<number, number>;
  selected: number;
  onSelect: (level: number) => void;
}

/** Смуга рівнів I…X під сценою: скільки юнітів кожного рівня, обраний — підсвічений. */
export function LevelStrip({ unitType, maxLevel, ceiling, counts, selected, onSelect }: StripProps) {
  const selectedRef = useRef<HTMLButtonElement>(null);

  // На вузькому екрані смуга гортається — обраний рівень завжди в полі зору
  useEffect(() => {
    selectedRef.current?.scrollIntoView({ block: "nearest", inline: "center" });
  }, [selected]);

  return (
    // Десять плиток уміщаються в ширину табору; на телефоні лишається видима прокрутка, щоб дістати крайні
    <div className="flex gap-1.5 overflow-x-auto px-2 pb-2 pt-2 [scrollbar-width:thin] sm:justify-center">
      {Array.from({ length: maxLevel }, (_, index) => index + 1).map((level) => (
        <button
          key={level}
          ref={level === selected ? selectedRef : undefined}
          type="button"
          onClick={() => onSelect(level)}
          title={level > ceiling ? t("army:camp.levelLocked", { level: roman(level), townHall: level }) : t("army:camp.level", { level: roman(level) })}
          className={`flex shrink-0 flex-col items-center gap-0.5 transition ${level === selected ? "scale-110" : "opacity-80 hover:opacity-100"}`}
        >
          <span className={level === selected ? "drop-shadow-[0_0_6px_#fff]" : ""}>
            <UnitHex unitType={unitType} level={level} size={46} dimmed={level > ceiling} />
          </span>
          <span className="text-xs font-semibold tabular-nums text-white [text-shadow:0_1px_2px_#000]">
            {formatNumber(counts[level] ?? 0)}
          </span>
        </button>
      ))}
    </div>
  );
}

interface OrderProps {
  title: string;
  description: string;
  unitType: string;
  level: number;
  /** Повна тривалість за формулою беку, секунд — від неї рахується смуга; прискорення лише посуває її вперед. */
  totalSeconds: number;
  orderId: string;
  completesAt: string;
  speedUpCostGems: number;
  timer: SpeedUpTimer;
  onFinish: () => void;
  finishing: boolean;
  error: unknown;
}

/** Картка партії в черзі будівлі: прогрес, залишок, «Завершити» за gems і «Прискорення» предметами. */
export function OrderCard(props: OrderProps) {
  const now = useNow();
  const catalog = useCatalog();
  const left = Math.max(0, Date.parse(props.completesAt) - now);
  const total = props.totalSeconds * 1_000;
  const progress = total > 0 ? Math.min(1, Math.max(0, 1 - left / total)) : 1;
  const gems = catalog.speedUpCost(props.timer, props.completesAt, now, props.speedUpCostGems);

  return (
    <div className="space-y-3">
      <ErrorBanner error={props.error} />
      <div className="flex items-center gap-3 rounded-xl bg-[#2a4f8c] p-3 ring-1 ring-[#3d6aae]">
        <UnitHex unitType={props.unitType} level={props.level} size={52} />
        <div className="min-w-0 flex-1 space-y-1">
          <div className="relative h-7 overflow-hidden rounded-md bg-slate-900/60 ring-1 ring-slate-950/50">
            <div className="h-full bg-gradient-to-b from-lime-400 to-green-600" style={{ width: `${progress * 100}%` }} />
            <span className="absolute inset-0 flex items-center justify-between px-2 text-sm font-semibold text-white [text-shadow:0_1px_2px_#000]">
              <span>{props.title}</span>
              <span className="tabular-nums">{formatRemaining(props.completesAt, now)}</span>
            </span>
          </div>
          <p className="text-center text-sm text-sky-100">{props.description}</p>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        {gems > 0 ? (
          <button
            type="button"
            onClick={props.onFinish}
            disabled={props.finishing}
            className="rounded-xl bg-gradient-to-b from-amber-300 to-orange-500 py-1.5 text-white shadow ring-1 ring-amber-200 [text-shadow:0_1px_1px_#0006] hover:brightness-110 disabled:opacity-50"
          >
            <span className="block text-xs font-semibold">{t("army:camp.finish")}</span>
            <span className="block text-lg font-bold leading-6">💎 {formatNumber(gems)}</span>
          </button>
        ) : (
          <span />
        )}
        <SpeedUpItemsButton
          timer={props.timer}
          targetId={props.orderId}
          completesAt={props.completesAt}
          label={t("army:camp.speedUps")}
          className="h-full w-full rounded-xl bg-gradient-to-b from-sky-400 to-blue-600 py-3 text-lg font-semibold text-white shadow ring-1 ring-sky-200 [text-shadow:0_1px_1px_#0006] hover:brightness-110"
        />
      </div>
    </div>
  );
}

/** Ціна партії: сума кроків рівня × кількість, по кожному ресурсу (§5.2 GDD). */
function costLine(unit: CatalogUnit, fromLevel: number, toLevel: number, count: number, resourceName: (key: string) => string) {
  return unit.cost
    .map((line) => {
      const perUnit = cumulativeUnitLevelCost(line.amount, fromLevel, toLevel, unit.levelUpCostGrowth);
      return `${formatNumber(perUnit * count)} ${resourceName(line.resource)}`;
    })
    .join(" · ");
}

/** Повзунок і поле кількості партії: від 1 до ліміту будівлі. */
function CountPicker({ value, max, onChange }: { value: number; max: number; onChange: (next: number) => void }) {
  const clamp = (next: number) => onChange(Math.min(max, Math.max(1, Number.isFinite(next) ? Math.trunc(next) : 1)));

  return (
    <div className="flex items-center gap-3">
      <input type="range" min={1} max={max} value={value} onChange={(event) => clamp(Number(event.target.value))} className="flex-1 accent-lime-400" />
      <input
        type="number"
        min={1}
        max={max}
        value={value}
        onChange={(event) => clamp(Number(event.target.value))}
        className="w-20 rounded-lg border border-[#3d6aae] bg-slate-900/60 px-2 py-1 text-right text-white"
      />
    </div>
  );
}

interface TrainProps {
  unit: CatalogUnit;
  level: number;
  maxBatch: number;
  /** Причина, з якої партію зараз не прийняти; null — можна тренувати. */
  blocked: string | null;
  pending: boolean;
  error: unknown;
  onTrain: (count: number) => void;
}

/** Нова партія на обраному рівні — тренування з нуля коштує суму кроків від 1 (§5.2 GDD). */
export function TrainForm({ unit, level, maxBatch, blocked, pending, error, onTrain }: TrainProps) {
  const catalog = useCatalog();
  const [count, setCount] = useState(maxBatch);
  const amount = Math.min(count, maxBatch);
  const seconds = cumulativeUnitLevelCost(unit.baseTrainSeconds, 1, level + 1, unit.trainTimeGrowth) * amount;

  return (
    <div className="space-y-3 rounded-xl bg-[#2a4f8c] p-3 ring-1 ring-[#3d6aae]">
      <ErrorBanner error={error} />
      <div className="flex items-baseline justify-between text-sm">
        <span className="font-semibold">{t("army:camp.training", { unit: unit.displayName, level: roman(level) })}</span>
        <span className="text-xs text-sky-200">{t("army:camp.batch", { max: maxBatch })}</span>
      </div>
      <CountPicker value={amount} max={maxBatch} onChange={setCount} />
      <p className="text-sm text-sky-100">
        {costLine(unit, 1, level + 1, amount, catalog.resourceName)} · ⏱ {formatDuration(seconds * 1_000)}
      </p>
      {blocked !== null && <p className="text-xs text-amber-300">{blocked}</p>}
      <div className="flex justify-end">
        <span data-tutorial={`train:${unit.key}`}>
          <GameButton onClick={() => onTrain(amount)} disabled={pending || blocked !== null} variant="accent">
            {t("army:camp.train")}
          </GameButton>
        </span>
      </div>
    </div>
  );
}

interface UpgradeProps {
  unit: CatalogUnit;
  /** Рівень, до якого качаємо — обраний на смузі. */
  toLevel: number;
  /** Нижчі рівні, з яких є кого качати: рівень → кількість. */
  sources: [number, number][];
  maxBatch: number;
  blocked: string | null;
  pending: boolean;
  error: unknown;
  onUpgrade: (fromLevel: number, count: number) => void;
}

/** Прокачка вже навчених до обраного рівня: платяться лише пройдені кроки (§5.2 GDD). */
export function UpgradeForm({ unit, toLevel, sources, maxBatch, blocked, pending, error, onUpgrade }: UpgradeProps) {
  const catalog = useCatalog();
  const [picked, setPicked] = useState<number | null>(null);
  const [count, setCount] = useState(maxBatch);

  const source = sources.find(([level]) => level === picked) ?? sources[sources.length - 1];

  if (source === undefined) {
    return (
      <p className="rounded-xl bg-[#2a4f8c] p-3 text-sm text-sky-100 ring-1 ring-[#3d6aae]">
        {t("army:camp.nothingToLevel", { level: roman(toLevel) })}
      </p>
    );
  }

  const [fromLevel, available] = source;
  const limit = Math.max(1, Math.min(available, maxBatch));
  const amount = Math.min(count, limit);
  const seconds = cumulativeUnitLevelCost(unit.baseTrainSeconds, fromLevel, toLevel, unit.trainTimeGrowth) * amount;

  return (
    <div className="space-y-3 rounded-xl bg-[#2a4f8c] p-3 ring-1 ring-[#3d6aae]">
      <ErrorBanner error={error} />
      <p className="text-sm font-semibold">{t("army:camp.levelUpTo", { level: roman(toLevel) })}</p>
      <div className="flex flex-wrap gap-2">
        {sources.map(([level, have]) => (
          <button
            key={level}
            type="button"
            onClick={() => setPicked(level)}
            className={`rounded-lg px-2 py-1 text-xs font-semibold ring-1 ${
              level === fromLevel ? "bg-lime-500 text-slate-900 ring-lime-200" : "bg-slate-900/50 text-white ring-[#3d6aae]"
            }`}
          >
            {t("army:camp.fromLevel", { level: roman(level), count: formatNumber(have) })}
          </button>
        ))}
      </div>
      <CountPicker value={amount} max={limit} onChange={setCount} />
      <p className="text-sm text-sky-100">
        {costLine(unit, fromLevel, toLevel, amount, catalog.resourceName)} · ⏱ {formatDuration(seconds * 1_000)}
      </p>
      {blocked !== null && <p className="text-xs text-amber-300">{blocked}</p>}
      <div className="flex justify-end">
        <GameButton onClick={() => onUpgrade(fromLevel, amount)} disabled={pending || blocked !== null} variant="accent">
          {t("army:camp.levelUp")}
        </GameButton>
      </div>
    </div>
  );
}

const STATS: { key: "Attack" | "Defense" | "Speed" | "CarryCapacity"; icon: string; scales: boolean }[] = [
  { key: "Attack", icon: "⚔", scales: true },
  { key: "Defense", icon: "🛡", scales: true },
  { key: "Speed", icon: "👣", scales: false },
  { key: "CarryCapacity", icon: "🛒", scales: false },
];

/**
 * Показники юніта на обраному рівні. Смуга атаки й захисту — частка від найвищого рівня,
 * швидкості й вантажу — від найкращого серед усіх юнітів: так видно, чим тип сильний.
 */
export function UnitStats({ unit, level, maxLevel }: { unit: CatalogUnit; level: number; maxLevel: number }) {
  const catalog = useCatalog();
  const best = (key: string) => Math.max(...catalog.units.map((other) => other.stats[key] ?? 0), 1);

  return (
    <div className="space-y-3">
      <h3 className="flex items-center gap-2 text-sm text-sky-200">
        <span className="h-px flex-1 bg-sky-300/40" />
        {t("army:camp.stats")}
        <span className="h-px flex-1 bg-sky-300/40" />
      </h3>
      <dl className="grid grid-cols-2 gap-x-6 gap-y-3">
        {STATS.filter((stat) => unit.stats[stat.key] !== undefined).map((stat) => {
          const base = unit.stats[stat.key] ?? 0;
          const value = stat.scales ? base * unitStatMultiplier(level, catalog.unitLevelStatBonus) : base;
          const full = stat.scales ? base * unitStatMultiplier(maxLevel, catalog.unitLevelStatBonus) : best(stat.key);

          return (
            <div key={stat.key} className="flex items-center gap-2">
              <span aria-hidden className="w-6 text-center text-lg">
                {stat.icon}
              </span>
              <div className="flex-1">
                <div className="flex justify-between text-sm">
                  <dt>{t(`army:unitStat.${stat.key}`)}</dt>
                  <dd className="font-semibold tabular-nums">{Math.round(value * 10) / 10}</dd>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-slate-900/60">
                  <div className="h-full bg-gradient-to-r from-green-500 to-lime-400" style={{ width: `${Math.min(1, value / full) * 100}%` }} />
                </div>
              </div>
            </div>
          );
        })}
      </dl>
    </div>
  );
}
