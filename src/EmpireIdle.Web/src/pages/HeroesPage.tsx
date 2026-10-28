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
import { parseRosterSort, ROSTER_SORTS, sortRoster } from "../lib/heroOrder";
import { formatNumber } from "../lib/format";
import { t } from "i18next";

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
  // Порядок ростера живе в адресі: картка героя гортає стрілками в тому ж порядку
  const sort = parseRosterSort(params.get("sort"));

  if (heroes.isPending) {
    return <p className="text-slate-500">{t("heroes:list.loading")}</p>;
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

  const roster = sortRoster(heroes.data.heroes, (key) => catalog.hero(key)?.rank, sort);
  const free = roster.filter((hero) => hero.state === "Idle").length;

  // Універсальні осколки за рідкістю (GDD §6.1): предмет-осколок має рідкість героїв, яким він підходить
  const universal = (rarity: string) =>
    inventory.data?.items.find(
      (item) => catalog.item(item.itemKey)?.type === "universalshard" && item.rarity.toLowerCase() === rarity.toLowerCase(),
    )?.count ?? 0;

  const tabs: GameTab<Tab>[] = [
    { key: "heroes", label: t("heroes:list.tabHeroes", { count: roster.length }) },
    { key: "camp", label: t("heroes:list.tabCamp") },
  ];

  return (
    <div className="space-y-4">
      <GameScreen
        title={t("heroes:list.title")}
        actions={
          <p className="text-right text-xs text-sky-100/80">
            {t("heroes:list.summary", {
              experience: formatNumber(heroes.data.experience),
              free,
              marches: heroes.data.marchCapacity,
            })}{" "}
            <Link to="/heroes/codex" className="text-sky-200 hover:underline">
              {t("heroes:list.codex")}
            </Link>
          </p>
        }
      >
        <GameTabs tabs={tabs} value={tab} onChange={(next) => setParams({ tab: next, sort }, { replace: true })} />
        <GamePanel className="min-h-[30vh]">
          {tab === "heroes" &&
            (roster.length === 0 ? (
              <p className="py-8 text-center text-sm text-sky-100/80">
                {t("heroes:list.empty")}
              </p>
            ) : (
              <div className="space-y-3">
              <div className="flex flex-wrap items-center gap-1.5 text-xs">
                <span className="text-sky-100/80">{t("heroes:list.sortBy")}</span>
                {ROSTER_SORTS.map((option) => (
                  <button
                    key={option}
                    type="button"
                    onClick={() => setParams({ tab, sort: option }, { replace: true })}
                    className={`rounded-full px-3 py-1 font-semibold ring-1 transition ${
                      option === sort ? "bg-[#e8eef8] text-[#1d3b6f] ring-white" : "bg-[#2a4f8c] text-white ring-[#3d6aae] hover:bg-[#3a63a6]"
                    }`}
                  >
                    {t(`heroes:rosterSort.${option}`)}
                  </button>
                ))}
              </div>
              <TileGrid>
                {roster.map((hero, index) => {
                  const config = catalog.hero(hero.heroKey);

                  return (
                    <Tile
                      key={hero.id}
                      rarity={config?.rank}
                      onClick={() => void navigate(`/heroes/${hero.id}?sort=${sort}`)}
                      title={catalog.heroName(hero.heroKey)}
                      tutorial={index === 0 ? "hero-card" : undefined}
                      dimmed={hero.state === "Wounded"}
                      corner={<RoleBadge role={config?.class} size={18} />}
                      top={
                        <span className="pl-4">
                          {t("heroes:list.level", { level: hero.effectiveLevel })}
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
              </div>
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
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("heroes:list.shards")}</h2>
            <ShardsPanel shards={heroes.data.shards} busy={busy} onSummon={(heroKey) => summon.mutate(heroKey)} />
          </section>

          <section className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("heroes:list.universal")}</h2>
            <p className="text-sm text-slate-600">
              {t("heroes:list.universalText", {
                common: universal("Common"),
                rare: universal("Rare"),
                unique: universal("Unique"),
              })}
            </p>
            {/* Обмін відкриває сервер, коли всі герої рідкості мають усі зірки — інакше відмова з поясненням */}
            <div className="flex flex-wrap gap-2 text-sm">
              <button
                type="button"
                onClick={() => upgradeShards.mutate({ from: "Common", count: 1 })}
                disabled={busy || universal("Common") < 100}
                className="rounded-lg border border-slate-300 px-3 py-1 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                {t("heroes:list.upgradeCommon")}
              </button>
              <button
                type="button"
                onClick={() => upgradeShards.mutate({ from: "Rare", count: 1 })}
                disabled={busy || universal("Rare") < 300}
                className="rounded-lg border border-slate-300 px-3 py-1 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                {t("heroes:list.upgradeRare")}
              </button>
            </div>
          </section>
        </>
      )}
    </div>
  );
}
