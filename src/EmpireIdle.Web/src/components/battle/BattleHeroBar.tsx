import { useMemo, type ReactElement } from "react";
import type { BattleReplay } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import HeroPortrait from "../heroes/HeroPortrait";
import { skillClock, skillTimeline } from "./battleReplay";

interface Props {
  replay: BattleReplay;
  tick: number;
  health: number[];
  /** Ручний бій: готовий герой світиться й натискається. */
  manual: boolean;
  onCast: (hero: number) => void;
}

/**
 * Герої гравця під полем бою: портрет із сектором перезарядки активного вміння й секундами до нього,
 * смужка здоров'я під ним. У ручному бою готове вміння світиться, доки гравець не натисне портрет.
 */
export default function BattleHeroBar({ replay, tick, health, manual, onCast }: Props): ReactElement {
  const catalog = useCatalog();

  // Активне вміння кожного героя — його відлік і натискання; лінія часу перераховується лише з новим логом
  const heroes = useMemo(
    () =>
      replay.fighters
        .filter((fighter) => fighter.attacker && fighter.leader == null)
        .map((fighter) => {
          const config = catalog.hero(fighter.heroKey ?? "");
          const active = fighter.skills.findIndex((key) => config?.skills.find((skill) => skill.key === key)?.kind === "Active");

          const cooldown = Math.round(((fighter.skillCooldowns[active] ?? 0) * 1000) / replay.tickMilliseconds);

          return { fighter, config, cooldown, timeline: active < 0 ? [] : skillTimeline(replay, fighter.index, active) };
        }),
    [replay, catalog],
  );

  const seconds = (ticks: number) => Math.ceil((ticks * replay.tickMilliseconds) / 1000);

  return (
    <ol className="grid grid-cols-5 gap-2">
      {heroes.map(({ fighter, config, cooldown, timeline }) => {
        const share = Math.max(0, (health[fighter.index] ?? 0) / Math.max(1, fighter.maxHealth));
        const dead = share <= 0;
        const clock = dead ? null : skillClock(timeline, tick, cooldown);
        const ready = manual && clock?.ready === true;
        const cooling = clock !== null && !clock.ready ? clock.remaining / clock.total : 0;

        return (
          <li key={fighter.index} className={`mx-auto flex w-16 flex-col gap-1 ${dead ? "opacity-40 grayscale" : ""}`}>
            <button
              type="button"
              disabled={!ready}
              onClick={() => onCast(fighter.index)}
              title={catalog.heroName(fighter.heroKey ?? "")}
              className={`relative flex h-16 w-16 items-center justify-center overflow-hidden rounded-lg ring-1 ${
                ready ? "animate-pulse ring-4 ring-amber-300" : "ring-white/20"
              }`}
            >
              <HeroPortrait heroKey={fighter.heroKey ?? ""} heroClass={config?.class} rank={config?.rank} tier={config?.nativeTier ?? 1} size={64} />
              {cooling > 0 && clock !== null && (
                // Сектор перезарядки: темна частина — скільки ще чекати
                <span
                  className="absolute inset-0 flex items-center justify-center text-lg font-bold text-white"
                  style={{ background: `conic-gradient(rgba(15, 23, 42, 0.7) ${cooling * 360}deg, transparent 0)` }}
                >
                  {seconds(clock.remaining)}
                </span>
              )}
            </button>
            <span className="h-1.5 w-full rounded bg-slate-900">
              <span className="block h-full rounded bg-emerald-400" style={{ width: `${share * 100}%` }} />
            </span>
          </li>
        );
      })}
    </ol>
  );
}
