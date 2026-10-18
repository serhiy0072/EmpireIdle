import type { ReactElement } from "react";
import type { BattleReplay } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { EventKind } from "./battleReplay";

interface HeroLine {
  index: number;
  heroKey: string;
  dealt: number;
  taken: number;
  healed: number;
}

/**
 * Підсумок по героях гравця з логу: скільки завдав разом зі своїми конвоями, скільки прийняв сам
 * і скільки вилікував. Найкращий — за завданою шкодою: так гравець бачить, хто тягнув бій.
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

  replay.events.kinds.forEach((kind, i) => {
    const source = replay.events.sources[i] ?? 0;
    const target = replay.events.targets[i] ?? 0;
    const amount = replay.events.amounts[i] ?? 0;

    if (kind === EventKind.Hit && source !== target) {
      const dealer = lines.get(heroOf(source));
      if (dealer !== undefined) dealer.dealt += amount;
    }

    if (kind === EventKind.Hit) {
      const victim = lines.get(target);
      if (victim !== undefined) victim.taken += amount;
    }

    if (kind === EventKind.Heal) {
      const healer = lines.get(heroOf(source));
      if (healer !== undefined) healer.healed += amount;
    }
  });

  return [...lines.values()];
}

export default function BattleSummary({ replay }: { replay: BattleReplay }): ReactElement {
  const catalog = useCatalog();
  const lines = summarize(replay);
  const best = lines.reduce<HeroLine | null>((top, line) => (top === null || line.dealt > top.dealt ? line : top), null);
  const format = (value: number) => Math.round(value).toLocaleString("uk-UA");

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
