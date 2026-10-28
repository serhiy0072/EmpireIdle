import type { BeastResponse } from "../../lib/queries/beasts";
import { formatRemaining } from "../../lib/time";
import { GameButton } from "../game/GameUi";
import { beastArtUrl } from "./beastArt";
import { passiveLine } from "./beastText";
import { formatNumber } from "../../lib/format";

interface FeedProps {
  beast: BeastResponse;
  feedLeft: number;
  busy: boolean;
  onFeed: (count: number) => void;
}

/**
 * Рівень і годування ласощами (GDD §5.10). Кнопки замість поля з числом (рішення 21.10.2026):
 * зайве сервер не з'їсть — лишиться в рюкзаку.
 */
export function BeastFeedPanel({ beast, feedLeft, busy, onFeed }: FeedProps) {
  const capped = beast.level >= beast.maxLevel;
  const toNext = beast.experienceToNext - beast.experience;
  const options = [
    { label: "+1", count: 1 },
    { label: "+10", count: 10 },
    { label: "До рівня", count: Math.min(feedLeft, toNext) },
    { label: "Максимум", count: feedLeft },
  ];

  return (
    <div className="space-y-3 rounded-xl bg-[#1d3b6f] p-3 ring-1 ring-[#335c9a]">
      <div className="flex items-center gap-3">
        <div className="flex h-14 w-14 shrink-0 flex-col items-center justify-center rounded-full bg-gradient-to-b from-sky-400 to-blue-700 ring-2 ring-sky-200">
          <span className="text-[10px] leading-none text-sky-100">Рівень</span>
          <span className="text-xl font-black leading-none">{beast.level}</span>
        </div>
        <div className="min-w-0 flex-1 space-y-1">
          <p className="text-sm text-sky-100">
            Ранг {beast.rank} · стеля рівня {beast.maxLevel} · {passiveLine(beast)}
          </p>
          <div className="h-2.5 overflow-hidden rounded-full bg-black/30">
            <div
              className="h-full bg-gradient-to-r from-amber-300 to-amber-500"
              style={{ width: capped ? "100%" : `${Math.min(100, (beast.experience / beast.experienceToNext) * 100)}%` }}
            />
          </div>
          <p className="text-xs text-sky-200/80">
            {capped
              ? "Стеля рангу: далі веде лише ще один такий самий звір"
              : `До наступного рівня — ${formatNumber(toNext)} ласощів · у рюкзаку ${formatNumber(feedLeft)}`}
          </p>
        </div>
      </div>

      {!capped && (
        <div className="grid grid-cols-4 gap-2">
          {options.map((option) => (
            <GameButton
              key={option.label}
              onClick={() => onFeed(option.count)}
              disabled={busy || option.count < 1 || feedLeft < option.count}
              variant="accent"
              title={`${formatNumber(option.count)} ласощів`}
            >
              {option.label}
            </GameButton>
          ))}
        </div>
      )}
    </div>
  );
}

interface PassiveProps {
  beast: BeastResponse;
  now: number;
  busy: boolean;
  onActivate: () => void;
}

/** Пасивка за корм зі складу: увімкнути, а далі — дія й перезарядка. */
export function BeastPassivePanel({ beast, now, busy, onActivate }: PassiveProps) {
  const { passive } = beast;
  const active = passive.activeUntil != null && Date.parse(passive.activeUntil) > now;
  const cooling = passive.cooldownUntil != null && Date.parse(passive.cooldownUntil) > now;

  return (
    <div className="flex flex-wrap items-center gap-3 rounded-xl bg-[#1d3b6f] p-3 text-sm ring-1 ring-[#335c9a]">
      <p className="flex-1 text-sky-100">
        {passiveLine(beast)} на {Math.round(passive.durationMinutes / 60)} год
      </p>
      {active ? (
        <span className="font-semibold text-emerald-300">Діє ще {formatRemaining(passive.activeUntil!, now)}</span>
      ) : cooling ? (
        <span className="text-sky-200/80">Відпочиває ще {formatRemaining(passive.cooldownUntil!, now)}</span>
      ) : (
        <GameButton onClick={onActivate} disabled={busy}>
          Активувати за {formatNumber(passive.activationFodder)} корму
        </GameButton>
      )}
    </div>
  );
}

interface TilesProps {
  beasts: BeastResponse[];
  name: (beastKey: string) => string;
  onPick: (beastKey: string) => void;
}

/** Плитки звірів, як «Список питомців» на референсі: звір, рівень, назва; натиск — екран звіра. */
export function BeastTiles({ beasts, name, onPick }: TilesProps) {
  return (
    <div className="grid grid-cols-3 gap-2 sm:grid-cols-5">
      {beasts.map((beast) => (
        <button
          key={beast.beastKey}
          type="button"
          onClick={() => onPick(beast.beastKey)}
          className="relative flex flex-col items-center rounded-xl bg-gradient-to-b from-sky-200 to-emerald-200 p-1 text-slate-800 ring-2 ring-sky-300 hover:ring-amber-400"
        >
          <span className="absolute right-1.5 top-1 text-xs font-bold text-white [text-shadow:0_1px_2px_#000]">Ур. {beast.level}</span>
          <img src={beastArtUrl(beast.beastKey)} alt="" className="h-16 w-20 object-contain" />
          <span className="text-xs font-semibold">{name(beast.beastKey)}</span>
        </button>
      ))}
    </div>
  );
}
