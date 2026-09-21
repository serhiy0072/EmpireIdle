import { heroState, rankLabel, rankStyle, useCatalog } from "../lib/queries/catalog";
import type { HeroSummary } from "../lib/queries/heroes";

interface Props {
  hero: HeroSummary;
  selected: boolean;
  levelingUntil: string | null;
  onSelect: () => void;
}

/** Сузір'я крапками: число тут читається гірше, ніж заповнені кружки. */
function Constellation({ value, max }: { value: number; max: number }) {
  return (
    <span className="flex gap-0.5" title={`Сузір'я ${value}/${max}`}>
      {Array.from({ length: max }, (_, index) => (
        <span key={index} className={`h-1.5 w-1.5 rounded-full ${index < value ? "bg-amber-500" : "bg-slate-200"}`} />
      ))}
    </span>
  );
}

export default function HeroCard({ hero, selected, levelingUntil, onSelect }: Props) {
  const catalog = useCatalog();
  const config = catalog.hero(hero.heroKey);

  return (
    <button
      type="button"
      data-tutorial={selected ? "hero-card" : undefined}
      onClick={onSelect}
      className={`w-full rounded-xl border p-3 text-left transition ${
        selected ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white hover:border-slate-300"
      }`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <span className="font-medium text-slate-800">{catalog.heroName(hero.heroKey)}</span>
        <span className="text-xs text-slate-500">T{hero.tier}</span>
      </div>

      <div className="mt-1 flex items-center justify-between">
        <span className="text-sm text-slate-600">
          рів. {hero.level}
          <span className="text-slate-400"> / {hero.maxLevel}</span>
        </span>
        <Constellation value={hero.constellation} max={catalog.maxConstellation} />
      </div>

      <div className="mt-2 flex flex-wrap gap-1 text-xs">
        {config !== null && (
          <>
            <span className={`rounded px-2 py-0.5 ${rankStyle(config.rank)}`}>{rankLabel(config.rank)}</span>
            <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{config.class}</span>
          </>
        )}
        <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{heroState(hero.state)}</span>
        {hero.isLeader && <span className="rounded bg-amber-100 px-2 py-0.5 text-amber-800">Лідер</span>}
        {levelingUntil !== null && <span className="rounded bg-sky-100 px-2 py-0.5 text-sky-800">{levelingUntil}</span>}
      </div>
    </button>
  );
}
