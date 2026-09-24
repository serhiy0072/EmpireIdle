import { heroState, passivePercent, rankLabel, rankStyle, useCatalog } from "../lib/queries/catalog";
import { useNow } from "../hooks/useNow";
import { speedUpLabel } from "../lib/speedUp";
import type { components } from "../lib/schema";
import type { HeroSummary } from "../lib/queries/heroes";
import { formatRemaining } from "../lib/time";
import HeroPortrait from "./heroes/HeroPortrait";

type HeroLevelOrder = components["schemas"]["HeroLevelOrderSummary"];

interface Props {
  hero: HeroSummary;
  /** Черга одна на гравця: поки вона зайнята, інші герої качатись не можуть. */
  queueBusy: boolean;
  /** Активна прокачка саме цього героя; null — він не в черзі. */
  order: HeroLevelOrder | null;
  busy: boolean;
  onLevelUp: () => void;
  onSpeedUp: () => void;
  onEvolve: () => void;
  onAppointLeader: () => void;
  onHeal: () => void;
}

export default function HeroDetails({
  hero,
  queueBusy,
  order,
  busy,
  onLevelUp,
  onSpeedUp,
  onEvolve,
  onAppointLeader,
  onHeal,
}: Props) {
  const catalog = useCatalog();
  const now = useNow();
  const config = catalog.hero(hero.heroKey);

  const atLevelCap = hero.level >= hero.maxLevel;
  const atTierCap = hero.tier >= catalog.maxTier;
  const stationed = hero.stationedGarrisonId !== null && hero.stationedGarrisonId !== undefined;

  return (
    <aside className="rounded-xl border border-slate-200 bg-white p-4 space-y-4">
      <div>
        <div className="flex gap-3">
          <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={88} />

          <div className="min-w-0">
            <h2 className="text-lg font-medium text-slate-800">{catalog.heroName(hero.heroKey)}</h2>

            {config !== null && (
              <div className="mt-1 flex flex-wrap items-center gap-1 text-xs">
                <span className={`rounded px-2 py-0.5 ${rankStyle(config.rank)}`}>{rankLabel(config.rank)}</span>
                <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{config.class}</span>
                <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">швидкість {config.speed}</span>
              </div>
            )}
          </div>
        </div>

        <p className="mt-2 text-sm text-slate-500">
          Тір {hero.tier} з {catalog.maxTier} · рівень {hero.level} з {hero.maxLevel} · сузір'я {hero.constellation}/
          {catalog.maxConstellation}
        </p>
        <p className="mt-1 text-sm text-slate-600">{heroState(hero.state)}</p>

        {config?.description !== null && config?.description !== undefined && (
          <p className="mt-2 text-sm text-slate-500">{config.description}</p>
        )}
      </div>

      {config !== null && config.passives.length > 0 && (
        <section>
          <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Вміння</h3>
          <ul className="mt-2 space-y-2">
            {config.passives.map((passive) => {
              const percent = passivePercent(passive, hero.constellation);

              return (
                <li key={passive.key} className="text-sm">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className={percent === null ? "text-slate-400" : "text-slate-800"}>{passive.displayName}</span>
                    <span className={percent === null ? "text-xs text-slate-400" : "text-xs text-emerald-700"}>
                      {percent === null ? `з сузір'я ${passive.unlockConstellation}` : `+${percent.toFixed(1)}%`}
                    </span>
                  </div>
                  <p className="text-xs text-slate-500">
                    {passive.stat} · {passive.target === "all" ? "усе військо" : passive.target}
                  </p>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <div className="space-y-2">
        {order !== null && (
          <div className="flex items-center justify-between gap-2 rounded-lg bg-sky-50 px-3 py-2 text-sm">
            <span className="text-sky-900">
              До рівня {order.targetLevel}: {formatRemaining(order.completesAt, now)}
            </span>
            <button
              type="button"
              onClick={onSpeedUp}
              disabled={busy}
              className="rounded-lg border border-sky-300 bg-white px-2 py-0.5 text-xs text-sky-800 hover:bg-sky-100 disabled:opacity-50"
            >
              {speedUpLabel(catalog.speedUpCost(order.completesAt, now, order.speedUpCostGems))}
            </button>
          </div>
        )}

        <button
          type="button"
          onClick={onLevelUp}
          disabled={busy || queueBusy || atLevelCap}
          className="w-full rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          {atLevelCap ? "Рівень уперся в стелю" : queueBusy ? "Черга зайнята" : "Підняти рівень"}
        </button>

        <button
          type="button"
          onClick={onEvolve}
          disabled={busy || atTierCap}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          {atTierCap ? "Максимальний тір" : "Еволюція тіру"}
        </button>

        {stationed && !hero.isLeader && (
          <button
            type="button"
            onClick={onAppointLeader}
            disabled={busy}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Призначити лідером гарнізону
          </button>
        )}

        {hero.state === "Wounded" && (
          <button
            type="button"
            onClick={onHeal}
            disabled={busy}
            className="w-full rounded-lg border border-rose-300 bg-rose-50 px-3 py-2 text-sm text-rose-800 hover:bg-rose-100 disabled:opacity-50"
          >
            Вилікувати
          </button>
        )}
      </div>

      <p className="text-xs text-slate-400">Стеля рівня — нижча з двох: рівень ратуші й тір × 10.</p>
    </aside>
  );
}
