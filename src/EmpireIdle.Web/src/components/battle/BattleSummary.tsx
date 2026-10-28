import type { ReactElement } from "react";
import type { BattleReplay } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { EventKind } from "./battleReplay";
import { formatNumber } from "../../lib/format";

interface HeroLine {
  index: number;
  heroKey: string;
  dealt: number;
  taken: number;
  healed: number;
}

/**
 * Підсумок по героях гравця з логу: скільки завдав разом зі своїми конвоями, скільки прийняв сам
 * і скільки вилікував. Рахується різниця здоров'я цілі, а не сила удару: удар понад смерть,
 * щит і лікування понад максимум нічого не змінили. Найкращий — за завданою шкодою.
 */
function summarize(replay: BattleReplay): HeroLine[] {
  const lines = new Map<number, HeroLine>();

  replay.fighters
    .filter((fighter) => fighter.attacker && fighter.leader == null)
    .forEach((fighter) => lines.set(fighter.index, { index: fighter.index, heroKey: fighter.heroKey ?? "", dealt: 0, taken: 0, healed: 0 }));

  const heroOf = (index: number) => {
    const fighter = replay.fighters[index];
    return fighter?.leader ?? index;
  };

  const health = replay.fighters.map((fighter) => fighter.maxHealth);

  replay.events.kinds.forEach((kind, i) => {
    if (kind !== EventKind.Hit && kind !== EventKind.Heal) return;

    const source = replay.events.sources[i] ?? 0;
    const target = replay.events.targets[i] ?? 0;
    const before = health[target] ?? 0;
    const after = replay.events.healths[i] ?? before;
    health[target] = after;

    if (kind === EventKind.Heal) {
      const healer = lines.get(heroOf(source));
      if (healer !== undefined) healer.healed += Math.max(0, after - before);
      return;
    }

    const lost = Math.max(0, before - after);

    // Отрута пише героя і джерелом, і ціллю — це прийнята шкода, а не завдана
    if (source !== target) {
      const dealer = lines.get(heroOf(source));
      if (dealer !== undefined) dealer.dealt += lost;
    }

    const victim = lines.get(target);
    if (victim !== undefined) victim.taken += lost;
  });

  return [...lines.values()];
}

export default function BattleSummary({ replay }: { replay: BattleReplay }): ReactElement {
  const catalog = useCatalog();
  const lines = summarize(replay);
  const best = lines.reduce<HeroLine | null>((top, line) => (top === null || line.dealt > top.dealt ? line : top), null);
  const format = (value: number) => formatNumber(Math.round(value));

  return (
    <table className="w-full text-left text-xs">
      <thead className="text-sky-200">
        <tr>
          <th className="py-1">Герой</th>
          <th className="text-right">Завдав</th>
          <th className="text-right">Прийняв</th>
          <th className="text-right">Вилікував</th>
        </tr>
      </thead>
      <tbody>
        {lines.map((line) => (
          <tr key={line.index} className={line === best ? "font-semibold text-amber-200" : ""}>
            <td className="py-0.5">
              {catalog.heroName(line.heroKey)}
              {line === best && " · найкращий"}
            </td>
            <td className="text-right">{format(line.dealt)}</td>
            <td className="text-right">{format(line.taken)}</td>
            <td className="text-right">{format(line.healed)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
