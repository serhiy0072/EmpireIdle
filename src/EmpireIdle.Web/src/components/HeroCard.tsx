import { heroName, heroState } from "../lib/heroNames";
import type { HeroSummary } from "../lib/queries/heroes";

interface Props {
  hero: HeroSummary;
  selected: boolean;
  levelingUntil: string | null;
  onSelect: () => void;
}

/** Сузір'я 0–6 крапками: число тут читається гірше, ніж заповнені кружки. */
function Constellation({ value }: { value: number }) {
  return (
    <span className="flex gap-0.5" title={`Сузір'я ${value}/6`}>
      {Array.from({ length: 6 }, (_, index) => (
        <span
          key={index}
          className={`h-1.5 w-1.5 rounded-full ${index < value ? "bg-amber-500" : "bg-slate-200"}`}
        />
      ))}
    </span>
  );
}

export default function HeroCard({ hero, selected, levelingUntil, onSelect }: Props) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={`w-full rounded-xl border p-3 text-left transition ${
        selected ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white hover:border-slate-300"
      }`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-medium text-slate-800">{heroName(hero.heroKey)}</span>
        <span className="text-xs text-slate-500">T{hero.tier}</span>
      </div>

      <div className="mt-1 flex items-center justify-between">
        <span className="text-sm text-slate-600">
          рів. {hero.level}
          <span className="text-slate-400"> / {hero.maxLevel}</span>
        </span>
        <Constellation value={hero.constellation} />
      </div>

      <div className="mt-2 flex flex-wrap gap-1 text-xs">
        <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{heroState(hero.state)}</span>
        {hero.isLeader && <span className="rounded bg-amber-100 px-2 py-0.5 text-amber-800">Лідер</span>}
        {levelingUntil !== null && <span className="rounded bg-sky-100 px-2 py-0.5 text-sky-800">{levelingUntil}</span>}
      </div>
    </button>
  );
}
