import { useState } from "react";
import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import HeroCard from "../components/HeroCard";
import HeroDetails from "../components/HeroDetails";
import ShardsPanel from "../components/ShardsPanel";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import { useInventory } from "../lib/queries/inventory";
import {
  useAdvanceHeroStar,
  useAppointLeader,
  useConvertUniversalShards,
  useEvolveHero,
  useHealHero,
  useHeroes,
  useLevelUpHero,
  useResetHeroLevel,
  useSummonHero,
  useUpgradeUniversalShards,
} from "../lib/queries/heroes";

export default function HeroesPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const heroes = useHeroes(playerId);
  const levelUp = useLevelUpHero(playerId);
  const evolve = useEvolveHero(playerId);
  const appointLeader = useAppointLeader(playerId);
  const heal = useHealHero(playerId);
  const summon = useSummonHero(playerId);
  const advanceStar = useAdvanceHeroStar(playerId);
  const convertShards = useConvertUniversalShards(playerId);
  const upgradeShards = useUpgradeUniversalShards(playerId);
  const inventory = useInventory(playerId);
  const catalog = useCatalog();
  const resetLevel = useResetHeroLevel(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  if (heroes.isPending) {
    return <p className="text-slate-500">Завантаження героїв…</p>;
  }

  if (heroes.isError) {
    return <ErrorBanner error={heroes.error} />;
  }

  const busy =
    levelUp.isPending ||
    evolve.isPending ||
    appointLeader.isPending ||
    heal.isPending ||
    summon.isPending ||
    advanceStar.isPending ||
    convertShards.isPending ||
    upgradeShards.isPending ||
    resetLevel.isPending;

  const failure =
    levelUp.error ??
    evolve.error ??
    appointLeader.error ??
    heal.error ??
    summon.error ??
    advanceStar.error ??
    convertShards.error ??
    upgradeShards.error ??
    resetLevel.error;

  const selected = heroes.data.heroes.find((hero) => hero.id === selectedId) ?? heroes.data.heroes[0] ?? null;
  const free = heroes.data.heroes.filter((hero) => hero.state === "Idle").length;

  // Універсальні осколки за рідкістю (GDD §6.1): предмет-осколок має рідкість героїв, яким він підходить
  const universal = (rarity: string) =>
    inventory.data?.items.find(
      (item) => catalog.item(item.itemKey)?.type === "universalshard" && item.rarity.toLowerCase() === rarity.toLowerCase(),
    )?.count ?? 0;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Герої</h1>
        <p className="text-sm text-slate-500">
          Досвід героїв: {heroes.data.experience.toLocaleString("uk-UA")} · вільних: {free} · стеля маршів:{" "}
          {heroes.data.marchCapacity} ·{" "}
          <Link to="/heroes/codex" className="text-emerald-700 hover:underline">
            кодекс
          </Link>
        </p>
      </div>

      <ErrorBanner error={failure} />

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div className="space-y-4">
          {heroes.data.heroes.length === 0 ? (
            <p className="text-sm text-slate-500">Героїв ще немає — купіть уламки звичайного героя за золото або крутіть банери.</p>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {heroes.data.heroes.map((hero) => (
                <HeroCard
                  key={hero.id}
                  hero={hero}
                  selected={selected?.id === hero.id}
                  onSelect={() => setSelectedId(hero.id)}
                />
              ))}
            </div>
          )}

          <section className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Уламки</h2>
            <ShardsPanel shards={heroes.data.shards} busy={busy} onSummon={(heroKey) => summon.mutate(heroKey)} />
          </section>

          <section className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Універсальні осколки</h2>
            <p className="text-sm text-slate-600">
              Звичайні {universal("Common")} · рідкісні {universal("Rare")} · унікальні {universal("Unique")}. Ідуть у
              відкритого героя своєї рідкості 1:1.
            </p>
            {/* Обмін відкриває сервер, коли всі герої рідкості мають усі зірки — інакше відмова з поясненням */}
            <div className="flex flex-wrap gap-2 text-sm">
              <button
                type="button"
                onClick={() => upgradeShards.mutate({ from: "Common", count: 1 })}
                disabled={busy || universal("Common") < 100}
                className="rounded-lg border border-slate-300 px-3 py-1 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                100 звичайних → 1 рідкісний
              </button>
              <button
                type="button"
                onClick={() => upgradeShards.mutate({ from: "Rare", count: 1 })}
                disabled={busy || universal("Rare") < 300}
                className="rounded-lg border border-slate-300 px-3 py-1 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                300 рідкісних → 1 унікальний
              </button>
            </div>
          </section>
        </div>

        {selected !== null && (
          <HeroDetails
            hero={selected}
            experience={heroes.data.experience}
            universalShards={universal(catalog.hero(selected.heroKey)?.rank ?? "")}
            busy={busy}
            onAdvanceStar={() => advanceStar.mutate(selected.id)}
            onConvertShards={(count) => convertShards.mutate({ heroKey: selected.heroKey, count })}
            onLevelUp={() => levelUp.mutate({ heroId: selected.id, levels: 1 })}
            onResetLevel={() => resetLevel.mutate(selected.id)}
            onEvolve={() => evolve.mutate(selected.id)}
            onAppointLeader={() => appointLeader.mutate(selected.id)}
            onHeal={() => heal.mutate(selected.id)}
          />
        )}
      </div>
    </div>
  );
}
