import { Link, Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GamePanel, GameScreen, GameTabs, RoleBadge, StarRow, type GameTab } from "../components/game/GameUi";
import HeroPortrait from "../components/heroes/HeroPortrait";
import HeroGearTab from "../components/heroes/screen/HeroGearTab";
import HeroLevelUp from "../components/heroes/screen/HeroLevelUp";
import HeroSkillsTab from "../components/heroes/screen/HeroSkillsTab";
import HeroStatsTab from "../components/heroes/screen/HeroStatsTab";
import { HOTKEY_PRIORITY, useHotkeys } from "../hooks/useHotkeys";
import { useSession } from "../hooks/useSession";
import { parseRosterSort, sortRoster } from "../lib/heroOrder";
import { rankLabel, useCatalog } from "../lib/queries/catalog";
import {
  useAdvanceHeroStar,
  useConvertUniversalShards,
  useEvolveHero,
  useHealHero,
  useHeroes,
  useLevelUpHero,
  useResetHeroLevel,
  useUpgradeHeroSkill,
} from "../lib/queries/heroes";
import { useEquip, useEquipBest, useInventory, useUnequip, useUnequipAll } from "../lib/queries/inventory";
import { formatNumber } from "../lib/format";
import { t } from "i18next";

type Tab = "stats" | "skills" | "gear";

const RANK_COLORS: Record<string, string> = {
  Common: "text-slate-200",
  Rare: "text-sky-300",
  Unique: "text-amber-300",
};

/**
 * Екран героя (рішення 08.10.2026, за референсом): великий портрет, сила, зірки, конвої й рівень,
 * стрілки між героями ростеру й три вкладки — показники, навички, спорядження.
 */
export default function HeroPage() {
  const { heroId = "" } = useParams();
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const navigate = useNavigate();
  const catalog = useCatalog();

  const heroes = useHeroes(playerId);
  const inventory = useInventory(playerId);
  const levelUp = useLevelUpHero(playerId);
  const advanceStar = useAdvanceHeroStar(playerId);
  const convertShards = useConvertUniversalShards(playerId);
  const evolve = useEvolveHero(playerId);
  const heal = useHealHero(playerId);
  const resetLevel = useResetHeroLevel(playerId);
  const upgradeSkill = useUpgradeHeroSkill(playerId);
  const equip = useEquip(playerId);
  const unequip = useUnequip(playerId);
  const unequipAll = useUnequipAll(playerId);
  const equipBest = useEquipBest(playerId);

  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as Tab | null) ?? "stats";

  // Той самий порядок, що в сітці героїв: стрілки гортають так, як гравець відсортував
  const sort = parseRosterSort(params.get("sort"));
  const roster = sortRoster(heroes.data?.heroes ?? [], (key) => catalog.hero(key)?.rank, sort);
  const position = roster.findIndex((hero) => hero.id === heroId);

  const go = (step: number) => {
    const next = roster[(position + step + roster.length) % roster.length];
    if (next !== undefined) void navigate(`/heroes/${next.id}?tab=${tab}&sort=${sort}`, { replace: true });
  };

  useHotkeys(
    { ArrowLeft: () => go(-1), ArrowRight: () => go(1) },
    { priority: HOTKEY_PRIORITY.neighbours, enabled: roster.length > 1 },
  );

  if (heroes.isPending) {
    return <p className="text-slate-500">{t("heroes:hero.loading")}</p>;
  }

  if (heroes.isError) {
    return <ErrorBanner error={heroes.error} />;
  }

  const hero = roster[position];
  const config = hero === undefined ? null : catalog.hero(hero.heroKey);

  if (hero === undefined) {
    return <Navigate to="/heroes" replace />;
  }

  const mutations = [levelUp, advanceStar, convertShards, evolve, heal, resetLevel, upgradeSkill, equip, unequip, unequipAll, equipBest];
  const busy = mutations.some((mutation) => mutation.isPending);
  const failure = mutations.map((mutation) => mutation.error).find((error) => error !== null) ?? null;

  const items = inventory.data?.items ?? [];
  const rank = config?.rank ?? "Common";
  const universal =
    items.find((item) => catalog.item(item.itemKey)?.type === "universalshard" && item.rarity.toLowerCase() === rank.toLowerCase())
      ?.count ?? 0;
  const books = (half: string) => {
    const book = config === null ? null : catalog.skillBook(config.class, config.rank, half);
    return book === null ? 0 : (items.find((item) => item.itemKey === book)?.count ?? 0);
  };

  const atLevelCap = hero.level >= hero.maxLevel;
  const starCost = hero.nextStarPartCost ?? null;
  const shardsMissing = starCost === null ? 0 : Math.max(0, starCost - hero.shards);

  const tabs: GameTab<Tab>[] = [
    { key: "stats", label: t("heroes:hero.tabStats") },
    { key: "skills", label: t("heroes:hero.tabSkills") },
    { key: "gear", label: t("heroes:hero.tabGear") },
  ];

  return (
    <div className="space-y-3">
      <GameScreen
        title={
          <span className="flex items-center gap-2">
            <Link to={`/heroes?sort=${sort}`} className="text-2xl leading-none text-sky-200 hover:text-white" aria-label={t("heroes:hero.toRoster")}>
              ←
            </Link>
            {catalog.heroName(hero.heroKey)}
            <span className="rounded bg-amber-400 px-1.5 text-xs font-bold text-amber-950">T{hero.tier}</span>
          </span>
        }
        actions={
          <Link to="/heroes/codex" className="text-sm text-sky-200 hover:underline">
            {t("heroes:list.codex")}
          </Link>
        }
      >
        <div className="relative flex flex-col items-center gap-3 pb-2">
          <div className="flex items-center gap-2">
            <span className={`text-3xl font-black italic drop-shadow ${RANK_COLORS[rank] ?? ""}`}>{rankLabel(rank)}</span>
            <RoleBadge role={config?.class} size={28} />
          </div>

          <div className="flex w-full items-center justify-between">
            <button type="button" onClick={() => go(-1)} className="px-3 text-4xl text-sky-200 hover:text-white" aria-label={t("heroes:hero.previous")}>
              ‹
            </button>
            <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={200} />
            <button type="button" onClick={() => go(1)} className="px-3 text-4xl text-sky-200 hover:text-white" aria-label={t("heroes:hero.next")}>
              ›
            </button>
          </div>

          <p className="rounded-full bg-gradient-to-r from-transparent via-rose-700/70 to-transparent px-8 py-0.5 text-lg font-bold">
            ⚔ {formatNumber(Math.round(hero.power))}
          </p>

          {/* Зірки за осколки героя (GDD §6.1): кожна частинка — +5% бойової міці */}
          <div className="flex items-center gap-4">
            <StarRow starParts={hero.starParts} partsPerStar={catalog.partsPerStar} maxStars={catalog.maxStars} />
            <GameButton
              onClick={() => advanceStar.mutate(hero.id)}
              disabled={busy || starCost === null || shardsMissing > 0}
              title={starCost === null ? t("heroes:hero.starsFull") : t("heroes:hero.starPart", { cost: starCost, have: hero.shards })}
            >
              ⬆
            </GameButton>
          </div>
          {shardsMissing > 0 && (
            <p className="text-xs text-sky-100/80">
              {t("heroes:hero.shardsMissing", { count: shardsMissing })}
              {universal > 0 && (
                <>
                  {" · "}
                  <button
                    type="button"
                    onClick={() => convertShards.mutate({ heroKey: hero.heroKey, count: Math.min(shardsMissing, universal) })}
                    disabled={busy}
                    className="text-emerald-300 underline disabled:opacity-50"
                  >
                    {t("heroes:hero.fillFromUniversal", { count: Math.min(shardsMissing, universal) })}
                  </button>
                </>
              )}
            </p>
          )}

          <div className="grid w-full max-w-md grid-cols-3 divide-x divide-[#335c9a] rounded-xl bg-[#16305e] py-2 text-center ring-1 ring-[#335c9a]">
            <div>
              <p className="text-xs text-sky-200/80">{t("heroes:hero.convoys")}</p>
              <p className="font-bold">{hero.convoys}</p>
            </div>
            <div>
              <p className="text-xs text-sky-200/80">{t("heroes:hero.level")}</p>
              <p className="text-2xl font-black">{hero.effectiveLevel}</p>
            </div>
            <div>
              <p className="text-xs text-sky-200/80">{t("heroes:hero.capacity")}</p>
              <p className="font-bold">{formatNumber(hero.convoyCapacity)}</p>
            </div>
          </div>

          {atLevelCap ? (
            <p className="text-sm text-sky-100/80">{t("heroes:hero.maxLevel")}</p>
          ) : (
            <HeroLevelUp
              playerId={playerId}
              hero={hero}
              pool={heroes.data.experience}
              items={items}
              busy={busy}
              onLevelUp={() => levelUp.mutate({ heroId: hero.id, levels: 1 })}
            />
          )}
        </div>

        <GameTabs tabs={tabs} value={tab} onChange={(next) => setParams({ tab: next, sort }, { replace: true })} />
        <GamePanel>
          {config === null ? (
            <p className="text-sm text-sky-100/80">{t("heroes:hero.unknown")}</p>
          ) : tab === "skills" ? (
            <HeroSkillsTab
              hero={hero}
              config={config}
              books={books}
              busy={busy}
              onUpgrade={(skillKey) => upgradeSkill.mutate({ heroId: hero.id, skillKey })}
            />
          ) : tab === "gear" ? (
            <HeroGearTab
              playerId={playerId}
              hero={hero}
              config={config}
              equipment={inventory.data?.equipment ?? []}
              items={inventory.data?.items ?? []}
              busy={busy}
              onEquip={(equipmentId) => equip.mutate({ heroId: hero.id, equipmentId })}
              onUnequip={(equipmentId) => unequip.mutate(equipmentId)}
              onUnequipAll={(equipmentIds) => unequipAll.mutate(equipmentIds)}
              onEquipBest={() => equipBest.mutate(hero.id)}
            />
          ) : (
            <HeroStatsTab
              hero={hero}
              config={config}
              busy={busy}
              onEvolve={() => evolve.mutate(hero.id)}
              onHeal={() => heal.mutate(hero.id)}
              onResetLevel={() => resetLevel.mutate(hero.id)}
            />
          )}
        </GamePanel>
      </GameScreen>

      <ErrorBanner error={failure} />

      {equipBest.data?.equipped === 0 && tab === "gear" && (
        <p className="text-sm text-slate-600">{t("heroes:hero.noBetterGear")}</p>
      )}
    </div>
  );
}
