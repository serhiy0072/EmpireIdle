import { Link, useNavigate, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { GamePanel, GameScreen, GameTabs, RoleBadge, StarRow, Tile, TileGrid, type GameTab } from "../components/game/GameUi";
import HeroPortrait from "../components/heroes/HeroPortrait";
import TrainingCampPanel from "../components/heroes/TrainingCampPanel";
import ShardsPanel from "../components/ShardsPanel";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import {
  useBuyCampSlot,
  useHeroes,
  usePlaceInCamp,
  useRemoveFromCamp,
  useResetHeroLevel,
  useSkipCampCooldown,
  useSummonHero,
  useUpgradeUniversalShards,
} from "../lib/queries/heroes";
import { useInventory } from "../lib/queries/inventory";
import { sortRoster } from "../lib/heroOrder";

type Tab = "heroes" | "camp";

/**
 * Ростер плитками (рішення 08.10.2026): найсильніші першими, як у грі. Клік відкриває
 * екран героя; табір — сусідня вкладка, уламки й обмін осколків — під ростером.
 */
export default function HeroesPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const navigate = useNavigate();
  const catalog = useCatalog();

  const heroes = useHeroes(playerId);
  const inventory = useInventory(playerId);
  const summon = useSummonHero(playerId);
  const upgradeShards = useUpgradeUniversalShards(playerId);
  const placeInCamp = usePlaceInCamp(playerId);
  const removeFromCamp = useRemoveFromCamp(playerId);
  const skipCampCooldown = useSkipCampCooldown(playerId);
  const buyCampSlot = useBuyCampSlot(playerId);
  const resetLevel = useResetHeroLevel(playerId);

  const [params, setParams] = useSearchParams();
  const tab: Tab = params.get("tab") === "camp" ? "camp" : "heroes";

  if (heroes.isPending) {
    return <p className="text-slate-500">Завантаження героїв…</p>;
  }

  if (heroes.isError) {
    return <ErrorBanner error={heroes.error} />;
  }

  const busy =
    summon.isPending ||
    upgradeShards.isPending ||
    placeInCamp.isPending ||
    removeFromCamp.isPending ||
    skipCampCooldown.isPending ||
    buyCampSlot.isPending ||
    resetLevel.isPending;
  const failure =
    summon.error ?? upgradeShards.error ?? placeInCamp.error ?? removeFromCamp.error ?? skipCampCooldown.error ?? buyCampSlot.error ?? resetLevel.error;

  const roster = sortRoster(heroes.data.heroes, (key) => catalog.hero(key)?.rank);
  const free = roster.filter((hero) => hero.state === "Idle").length;

  // Універсальні осколки за рідкістю (GDD §6.1): предмет-осколок має рідкість героїв, яким він підходить
  const universal = (rarity: string) =>
    inventory.data?.items.find(
      (item) => catalog.item(item.itemKey)?.type === "universalshard" && item.rarity.toLowerCase() === rarity.toLowerCase(),
    )?.count ?? 0;

  const tabs: GameTab<Tab>[] = [
    { key: "heroes", label: `Герої · ${roster.length}` },
    { key: "camp", label: "Навчальний табір" },
  ];

  return (
    <div className="space-y-4">
      <GameScreen
        title="Герої"
        actions={
          <p className="text-right text-xs text-sky-100/80">
            досвід {heroes.data.experience.toLocaleString("uk-UA")} · вільних {free} · маршів до {heroes.data.marchCapacity} ·{" "}
            <Link to="/heroes/codex" className="text-sky-200 hover:underline">
              кодекс
            </Link>
          </p>
        }
      >
        <GameTabs tabs={tabs} value={tab} onChange={(next) => setParams({ tab: next }, { replace: true })} />
        <GamePanel className="min-h-[30vh]">
          {tab === "heroes" &&
            (roster.length === 0 ? (
              <p className="py-8 text-center text-sm text-sky-100/80">
                Героїв ще немає — купіть уламки звичайного героя за золото або крутіть банери.
              </p>
            ) : (
              <TileGrid>
                {roster.map((hero, index) => {
                  const config = catalog.hero(hero.heroKey);

                  return (
                    <Tile
                      key={hero.id}
                      rarity={config?.rank}
                      onClick={() => void navigate(`/heroes/${hero.id}`)}
                      title={catalog.heroName(hero.heroKey)}
                      tutorial={index === 0 ? "hero-card" : undefined}
                      dimmed={hero.state === "Wounded"}
                      corner={<RoleBadge role={config?.class} size={18} />}
                      top={
                        <span className="pl-4">
                          Ур. {hero.effectiveLevel}
                          {hero.state === "Deployed" ? " ⚑" : hero.state === "Wounded" ? " ✚" : hero.campSlot != null ? " ⛺" : ""}
                        </span>
                      }
                      bottom={<StarRow starParts={hero.starParts} partsPerStar={catalog.partsPerStar} maxStars={catalog.maxStars} size={11} />}
                    >
                      <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={76} />
                    </Tile>
                  );
                })}
              </TileGrid>
            ))}

          {tab === "camp" && (
            <TrainingCampPanel
              camp={heroes.data.camp}
              heroes={heroes.data.heroes}
              busy={busy}
              onPlace={(heroId, slot) => placeInCamp.mutate({ heroId, slot })}
              onRemove={(heroId) => removeFromCamp.mutate(heroId)}
              onSkipCooldown={(slot) => skipCampCooldown.mutate(slot)}
              onBuySlot={() => buyCampSlot.mutate()}
              onResetLevel={(heroId) => resetLevel.mutate(heroId)}
            />
          )}
        </GamePanel>
      </GameScreen>

      <ErrorBanner error={failure} />

      {tab === "heroes" && (
        <>
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
        </>
      )}
    </div>
  );
}
