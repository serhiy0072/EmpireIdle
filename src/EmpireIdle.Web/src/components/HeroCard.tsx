import { heroState, rankLabel, rankStyle, useCatalog } from "../lib/queries/catalog";
import type { HeroSummary } from "../lib/queries/heroes";
import HeroPortrait from "./heroes/HeroPortrait";

interface Props {
  hero: HeroSummary;
  selected: boolean;
  onSelect: () => void;
}

/** Зірки крапками (GDD §6.1): повна — яскрава, та, що заповнюється, — бліда. */
function Stars({ parts, max, perStar }: { parts: number; max: number; perStar: number }) {
  const full = Math.floor(parts / perStar);

  return (
    <span className="flex gap-0.5" title={`Зірки ${full}/${max} · частинок ${parts % perStar}/${perStar}`}>
      {Array.from({ length: max }, (_, index) => (
        <span
          key={index}
          className={`h-1.5 w-1.5 rounded-full ${index < full ? "bg-amber-500" : index === full && parts % perStar > 0 ? "bg-amber-200" : "bg-slate-200"}`}
        />
      ))}
    </span>
  );
}

export default function HeroCard({ hero, selected, onSelect }: Props) {
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
      <div className="flex gap-3">
        <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={56} />

        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="truncate font-medium text-slate-800">{catalog.heroName(hero.heroKey)}</span>
            <span className="text-xs text-slate-500">T{hero.tier}</span>
          </div>

          <div className="mt-1 flex items-center justify-between">
            <span className="text-sm text-slate-600">
              рів. {hero.level}
              <span className="text-slate-400"> / {hero.maxLevel}</span>
            </span>
            <Stars parts={hero.starParts} max={catalog.maxStars} perStar={catalog.partsPerStar} />
          </div>
        </div>
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
      </div>
    </button>
  );
}
