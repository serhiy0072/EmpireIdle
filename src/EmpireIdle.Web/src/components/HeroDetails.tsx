import { heroName, heroState } from "../lib/heroNames";
import type { HeroSummary } from "../lib/queries/heroes";

interface Props {
  hero: HeroSummary;
  /** Черга одна на гравця: поки вона зайнята, інші герої качатись не можуть. */
  queueBusy: boolean;
  busy: boolean;
  onLevelUp: () => void;
  onEvolve: () => void;
  onAppointLeader: () => void;
  onHeal: () => void;
}

export default function HeroDetails({ hero, queueBusy, busy, onLevelUp, onEvolve, onAppointLeader, onHeal }: Props) {
  const atLevelCap = hero.level >= hero.maxLevel;
  const stationed = hero.stationedGarrisonId !== null && hero.stationedGarrisonId !== undefined;

  return (
    <aside className="rounded-xl border border-slate-200 bg-white p-4 space-y-4">
      <div>
        <h2 className="text-lg font-medium text-slate-800">{heroName(hero.heroKey)}</h2>
        <p className="text-sm text-slate-500">
          Тір {hero.tier} · рівень {hero.level} з {hero.maxLevel} · сузір'я {hero.constellation}/6
        </p>
        <p className="mt-1 text-sm text-slate-600">{heroState(hero.state)}</p>
      </div>

      <div className="space-y-2">
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
          disabled={busy || hero.tier >= 3}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          Еволюція тіру
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

      <p className="text-xs text-slate-400">
        Стеля рівня — нижча з двох: рівень ратуші й тір × 10.
      </p>
    </aside>
  );
}
