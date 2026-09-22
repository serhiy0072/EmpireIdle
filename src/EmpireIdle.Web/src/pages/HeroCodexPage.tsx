import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import HeroPortrait from "../components/heroes/HeroPortrait";
import { useSession } from "../hooks/useSession";
import { rankLabel, rankStyle, useCatalog, type CatalogHero } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";

const RANK_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

const CLASS_LABELS: Record<string, string> = {
  warrior: "Воїн",
  knight: "Лицар",
  archer: "Лучник",
  mage: "Маг",
};

const STAT_LABELS: Record<string, string> = { Attack: "Атака", Defense: "Захист", Health: "Здоров'я" };

const TARGET_LABELS: Record<string, string> = {
  all: "усе військо",
  infantry: "піхота",
  archer: "лучники",
  cavalry: "кіннота",
  siege: "облогові",
};

/** Звідки береться герой: звичайні — за уламки, решта — з банерів. */
function source(hero: CatalogHero): string {
  return hero.summonShards > 0 ? `уламки · ${hero.summonShards} шт. по ${hero.shardPriceGold} золота` : "банери";
}

/**
 * Кодекс: усі герої гри з каталогу, наявні — позначені. Гравець бачить,
 * кого ще шукати на банерах, і за що саме кожен герой цінний.
 */
export default function HeroCodexPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const heroes = useHeroes(playerId);

  const [rank, setRank] = useState<string>("all");
  const [heroClass, setHeroClass] = useState<string>("all");
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const owned = useMemo(
    () => new Map((heroes.data?.heroes ?? []).map((hero) => [hero.heroKey, hero])),
    [heroes.data?.heroes],
  );

  const all = useMemo(
    () =>
      [...catalog.allHeroes]
        .filter((hero) => rank === "all" || hero.rank === rank)
        .filter((hero) => heroClass === "all" || hero.class === heroClass)
        .sort((a, b) => (RANK_ORDER[a.rank] ?? 9) - (RANK_ORDER[b.rank] ?? 9) || a.displayName.localeCompare(b.displayName, "uk")),
    [catalog.allHeroes, rank, heroClass],
  );

  if (!catalog.loaded) {
    return <p className="text-slate-500">Гортаємо кодекс…</p>;
  }

  if (heroes.isError) {
    return <ErrorBanner error={heroes.error} />;
  }

  const selected = all.find((hero) => hero.key === selectedKey) ?? all[0] ?? null;
  const ownedCount = catalog.allHeroes.filter((hero) => owned.has(hero.key)).length;
  const select = "rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700";

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Кодекс героїв</h1>
        <p className="text-sm text-slate-500">
          Зібрано {ownedCount} з {catalog.allHeroes.length} ·{" "}
          <Link to="/heroes" className="text-emerald-700 hover:underline">
            мої герої
          </Link>
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        <select value={rank} onChange={(event) => setRank(event.target.value)} aria-label="Ранг" className={select}>
          <option value="all">Усі ранги</option>
          <option value="Unique">Унікальні</option>
          <option value="Rare">Рідкісні</option>
          <option value="Common">Звичайні</option>
        </select>
        <select value={heroClass} onChange={(event) => setHeroClass(event.target.value)} aria-label="Клас" className={select}>
          <option value="all">Усі класи</option>
          {Object.entries(CLASS_LABELS).map(([key, label]) => (
            <option key={key} value={key}>
              {label}
            </option>
          ))}
        </select>
      </div>

      <div className="grid gap-4 lg:grid-cols-[3fr_2fr]">
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
          {all.map((hero) => {
            const mine = owned.get(hero.key);
            const isSelected = selected?.key === hero.key;

            return (
              <button
                key={hero.key}
                type="button"
                onClick={() => setSelectedKey(hero.key)}
                className={`flex items-center gap-3 rounded-xl border p-2 text-left transition ${
                  isSelected ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white hover:border-slate-300"
                }`}
              >
                <HeroPortrait
                  heroKey={hero.key}
                  heroClass={hero.class}
                  rank={hero.rank}
                  tier={mine?.tier ?? 1}
                  size={48}
                  className={mine === undefined ? "opacity-50 grayscale" : ""}
                />
                <div className="min-w-0">
                  <div className="truncate text-sm font-medium text-slate-800">{hero.displayName}</div>
                  <div className="flex flex-wrap gap-1 text-xs">
                    <span className={`rounded px-1.5 ${rankStyle(hero.rank)}`}>{rankLabel(hero.rank)}</span>
                    <span className="text-slate-500">{CLASS_LABELS[hero.class] ?? hero.class}</span>
                    {mine !== undefined && <span className="text-emerald-700">рів. {mine.level}</span>}
                  </div>
                </div>
              </button>
            );
          })}
          {all.length === 0 && <p className="text-sm text-slate-500">За цим фільтром героїв немає.</p>}
        </div>

        {selected !== null && (
          <aside className="h-fit space-y-3 rounded-xl border border-slate-200 bg-white p-4">
            <div className="flex gap-3">
              <HeroPortrait
                heroKey={selected.key}
                heroClass={selected.class}
                rank={selected.rank}
                tier={owned.get(selected.key)?.tier ?? 1}
                size={88}
                className={owned.has(selected.key) ? "" : "opacity-60 grayscale"}
              />
              <div className="min-w-0">
                <h2 className="text-lg font-medium text-slate-800">{selected.displayName}</h2>
                <div className="mt-1 flex flex-wrap gap-1 text-xs">
                  <span className={`rounded px-2 py-0.5 ${rankStyle(selected.rank)}`}>{rankLabel(selected.rank)}</span>
                  <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{CLASS_LABELS[selected.class] ?? selected.class}</span>
                  <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">швидкість {selected.speed}</span>
                </div>
                <p className="mt-1 text-xs text-slate-500">
                  {owned.has(selected.key) ? "У вашій залі героїв" : `Ще не зібрано · ${source(selected)}`}
                </p>
              </div>
            </div>

            {selected.description != null && selected.description !== "" && (
              <p className="text-sm text-slate-600">{selected.description}</p>
            )}

            <dl className="grid grid-cols-3 gap-1 text-sm">
              {Object.entries(selected.baseStats).map(([stat, value]) => (
                <div key={stat} className="rounded bg-slate-50 px-2 py-1">
                  <dt className="text-xs text-slate-500">{STAT_LABELS[stat] ?? stat}</dt>
                  <dd className="font-medium text-slate-800">
                    {value}
                    <span className="text-xs text-slate-400"> +{selected.statGrowth[stat] ?? 0}/рів.</span>
                  </dd>
                </div>
              ))}
            </dl>

            <section className="space-y-1">
              <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Вміння</h3>
              <ul className="space-y-1 text-sm">
                {selected.passives.map((passive) => (
                  <li key={passive.key} className="flex items-baseline justify-between gap-2">
                    <span className="text-slate-800">
                      {passive.displayName}
                      <span className="ml-1 text-xs text-slate-500">
                        {STAT_LABELS[passive.stat] ?? passive.stat} · {TARGET_LABELS[passive.target] ?? passive.target}
                      </span>
                    </span>
                    <span className="whitespace-nowrap text-xs text-slate-500">
                      +{passive.basePercent}%
                      {passive.percentPerConstellation > 0 && ` (+${passive.percentPerConstellation}% за сузір'я)`}
                      {passive.unlockConstellation > 0 && ` · з сузір'я ${passive.unlockConstellation}`}
                    </span>
                  </li>
                ))}
              </ul>
            </section>
          </aside>
        )}
      </div>
    </div>
  );
}
