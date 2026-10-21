import type { ReactElement } from "react";
import type { RewardConfig, TowerFloorView, TowerLeaderboard, TowerUpcomingFloor } from "../../lib/apiTypes";
import { classLabel } from "../../lib/heroClasses";
import { useCatalog } from "../../lib/queries/catalog";
import { rewardLabel } from "../../lib/questLabels";
import HeroPortrait from "../heroes/HeroPortrait";

/** Нагороди рядком: «270 золота · 5 💎». */
export function RewardLine({ rewards }: { rewards: RewardConfig[] }): ReactElement {
  const catalog = useCatalog();

  return <span>{rewards.map((reward) => rewardLabel(reward, catalog)).join(" · ")}</span>;
}

/**
 * Ворог наступного поверху: склад по доріжках, рівні й набір. Склад видно до бою — без цього
 * контргра складом не мала б сенсу.
 */
export function TowerFloorPanel({ floor }: { floor: TowerFloorView }): ReactElement {
  const catalog = useCatalog();

  return (
    <div className="space-y-3 rounded-xl bg-[#1d3b6f] p-3 ring-1 ring-[#335c9a]">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-base font-semibold">
          Поверх {floor.number}
          {floor.isBoss && <span className="ml-2 rounded bg-rose-600 px-1.5 py-0.5 text-xs">бос</span>}
        </h2>
        <p className="text-xs text-sky-200">
          {floor.archetypeName} · герої {floor.heroLevel} рів. · юніти {floor.unitLevel} рів. · {floor.setName}
        </p>
      </div>

      <ol className="grid grid-cols-5 gap-2">
        {floor.enemies.map((enemy) => (
          <li key={enemy.lane} className="flex flex-col items-center gap-1 text-center">
            <HeroPortrait heroKey={enemy.heroKey} heroClass={enemy.heroClass} rank={catalog.hero(enemy.heroKey)?.rank} tier={catalog.hero(enemy.heroKey)?.nativeTier ?? 1} size={52}
              className={enemy.boss ? "ring-2 ring-rose-400" : ""} />
            <span className="w-full truncate text-xs">{catalog.heroName(enemy.heroKey)}</span>
            <span className="text-[10px] text-sky-200">{classLabel(enemy.heroClass)}</span>
          </li>
        ))}
      </ol>

      <p className="text-sm">
        <span className="text-sky-200">Нагорода: </span>
        <RewardLine rewards={floor.rewards} />
      </p>
    </div>
  );
}

/** Стрічка найближчих поверхів: кожен 5-й і 10-й помітні, бо там нагорода більша. */
export function TowerStrip({ floors }: { floors: TowerUpcomingFloor[] }): ReactElement {
  return (
    <ol className="space-y-1">
      {floors.map((floor) => (
        <li
          key={floor.number}
          className={`flex items-center justify-between gap-2 rounded-lg px-3 py-1.5 text-sm ${
            floor.isBoss ? "bg-rose-900/60 ring-1 ring-rose-400" : floor.number % 5 === 0 ? "bg-amber-900/40 ring-1 ring-amber-300" : "bg-white/5"
          }`}
        >
          <span className="font-semibold">
            {floor.number}
            {floor.isBoss && " · бос"}
          </span>
          <span className="truncate text-right text-xs text-sky-100">
            <RewardLine rewards={floor.rewards} />
          </span>
        </li>
      ))}
    </ol>
  );
}

/** Рейтинг сервера: поверх і хто дійшов раніше; власний рядок — і поза топом. */
export function TowerLeaderboardPanel({ board, playerId }: { board: TowerLeaderboard; playerId: string }): ReactElement {
  if (board.entries.length === 0) return <p className="text-sm text-sky-200">Ще ніхто не пройшов жодного поверху — будьте першим.</p>;

  const mineInTop = board.entries.some((entry) => entry.playerId === playerId);

  return (
    <table className="w-full text-sm">
      <thead className="text-left text-xs text-sky-200">
        <tr>
          <th className="py-1">#</th>
          <th>Гравець</th>
          <th className="text-right">Поверх</th>
        </tr>
      </thead>
      <tbody>
        {board.entries.map((entry) => (
          <tr key={entry.playerId} className={entry.playerId === playerId ? "font-semibold text-amber-300" : ""}>
            <td className="py-1">{entry.rank}</td>
            <td className="truncate">{entry.playerName}</td>
            <td className="text-right">{entry.highestFloor}</td>
          </tr>
        ))}
        {board.mine != null && !mineInTop && (
          <tr className="border-t border-white/20 font-semibold text-amber-300">
            <td className="py-1">{board.mine.rank}</td>
            <td className="truncate">{board.mine.playerName}</td>
            <td className="text-right">{board.mine.highestFloor}</td>
          </tr>
        )}
      </tbody>
    </table>
  );
}
