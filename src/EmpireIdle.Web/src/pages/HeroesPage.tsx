import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import HeroCard from "../components/HeroCard";
import HeroDetails from "../components/HeroDetails";
import TrainingCampPanel from "../components/heroes/TrainingCampPanel";
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
  useUpgradeHeroSkill,
  usePlaceInCamp,
  useRemoveFromCamp,
  useSkipCampCooldown,
  useBuyCampSlot,
  useSummonHero,
  useUpgradeUniversalShards,
} from "../lib/queries/heroes";

type Tab = "heroes" | "camp";

const TABS: { key: Tab; label: string }[] = [
  { key: "heroes", label: "Герої" },
  { key: "camp", label: "Табір" },
];

const pill = (active: boolean) =>
  `rounded-lg px-3 py-1 text-sm ${active ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"}`;

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
  const upgradeSkill = useUpgradeHeroSkill(playerId);
  const placeInCamp = usePlaceInCamp(playerId);
  const removeFromCamp = useRemoveFromCamp(playerId);
  const skipCampCooldown = useSkipCampCooldown(playerId);
  const buyCampSlot = useBuyCampSlot(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [params, setParams] = useSearchParams();
  const tab = (TABS.find((t) => t.key === params.get("tab"))?.key ?? "heroes") as Tab;
  const setTab = (next: Tab) => setParams({ tab: next }, { replace: true });

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
    resetLevel.isPending ||
    upgradeSkill.isPending ||
    placeInCamp.isPending ||
    removeFromCamp.isPending ||
    skipCampCooldown.isPending ||
    buyCampSlot.isPending;

  const failure =
    levelUp.error ??
    evolve.error ??
    appointLeader.error ??
    heal.error ??
    summon.error ??
    advanceStar.error ??
    convertShards.error ??
    upgradeShards.error ??
    resetLevel.error ??
    upgradeSkill.error ??
    placeInCamp.error ??
    removeFromCamp.error ??
    skipCampCooldown.error ??
    buyCampSlot.error;

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

      <nav className="flex flex-wrap gap-1">
        {TABS.map((item) => (
          <button key={item.key} type="button" onClick={() => setTab(item.key)} className={pill(tab === item.key)}>
            {item.label}
          </button>
        ))}
      </nav>

      <ErrorBanner error={failure} />

      {tab === "camp" && (
        <TrainingCampPanel
          camp={heroes.data.camp}
          heroes={heroes.data.heroes}
          busy={busy}
          onPlace={(heroId, slot) => placeInCamp.mutate({ heroId, slot })}
          onRemove={(heroId) => removeFromCamp.mutate(heroId)}
          onSkipCooldown={(slot) => skipCampCooldown.mutate(slot)}
          onBuySlot={() => buyCampSlot.mutate()}
        />
      )}

      {tab === "heroes" && (
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
              skillBooks={(half) => {
                const config = catalog.hero(selected.heroKey);
                const book = config === null ? null : catalog.skillBook(config.class, config.rank, half);

                return book === null ? 0 : (inventory.data?.items.find((item) => item.itemKey === book)?.count ?? 0);
              }}
              onUpgradeSkill={(skillKey) => upgradeSkill.mutate({ heroId: selected.id, skillKey })}
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
      )}
    </div>
  );
}
