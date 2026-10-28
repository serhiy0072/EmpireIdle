import type { ReactElement } from "react";
import { classLabel } from "../../lib/heroClasses";
import { useCatalog } from "../../lib/queries/catalog";
import type { HeroSummary } from "../../lib/queries/heroes";
import { strongestSquad } from "../../lib/squad";
import { GameButton } from "../game/GameUi";
import HeroPortrait from "../heroes/HeroPortrait";
import { formatNumber } from "../../lib/format";

interface Props {
  heroes: HeroSummary[];
  lanes: (string | null)[];
  onChange: (lanes: (string | null)[]) => void;
}

/**
 * Загін за доріжками: клік по герою ставить його на першу вільну доріжку, клік по доріжці — знімає.
 * Глибину ставить клас — воїни у фронті, лицарі за ними, лучники в тилу; доріжка важить, бо ціль
 * шукається на найближчій. «Найсильніші» ставлять фронт на центральні доріжки.
 */
export default function SquadPicker({ heroes, lanes, onChange }: Props): ReactElement {
  const catalog = useCatalog();
  const byId = new Map(heroes.map((hero) => [hero.id, hero]));
  const strongest = [...heroes].sort((a, b) => b.power - a.power);

  const toggle = (heroId: string) => {
    if (lanes.includes(heroId)) {
      onChange(lanes.map((id) => (id === heroId ? null : id)));
      return;
    }

    const free = lanes.indexOf(null);
    if (free >= 0) onChange(lanes.map((id, lane) => (lane === free ? heroId : id)));
  };

  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between gap-2">
        <h3 className="text-sm font-semibold">Загін</h3>
        <GameButton variant="secondary" onClick={() => onChange(strongestSquad(heroes, lanes.length, (hero) => catalog.hero(hero.heroKey)?.class))}>
          Найсильніші
        </GameButton>
      </div>

      <ol className="grid grid-cols-5 gap-2">
        {lanes.map((heroId, lane) => {
          const hero = heroId === null ? undefined : byId.get(heroId);
          const config = hero === undefined ? null : catalog.hero(hero.heroKey);

          return (
            <li key={lane}>
              <button
                type="button"
                onClick={() => heroId !== null && toggle(heroId)}
                className="flex h-24 w-full flex-col items-center justify-center gap-1 rounded-lg bg-white/5 text-xs ring-1 ring-white/20 hover:bg-white/10"
              >
                {hero === undefined ? (
                  <span className="text-sky-300">доріжка {lane + 1}</span>
                ) : (
                  <>
                    <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={44} />
                    <span className="w-full truncate">{catalog.heroName(hero.heroKey)}</span>
                  </>
                )}
              </button>
            </li>
          );
        })}
      </ol>

      <ul className="grid grid-cols-4 gap-2 sm:grid-cols-6">
        {strongest.map((hero) => {
          const config = catalog.hero(hero.heroKey);
          const chosen = lanes.includes(hero.id);

          return (
            <li key={hero.id}>
              <button
                type="button"
                onClick={() => toggle(hero.id)}
                className={`flex w-full flex-col items-center gap-0.5 rounded-lg p-1 text-[11px] ${chosen ? "bg-sky-600/60 ring-1 ring-sky-200" : "bg-white/5 hover:bg-white/10"}`}
              >
                <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={40} />
                <span className="w-full truncate">{catalog.heroName(hero.heroKey)}</span>
                <span className="text-sky-200">
                  {classLabel(config?.class)} · {hero.effectiveLevel}
                </span>
                <span className="text-amber-200">⚔ {formatNumber(Math.round(hero.power))}</span>
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
