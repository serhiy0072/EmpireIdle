import { Link, Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GamePanel, GameScreen, GameTabs, RoleBadge, StarRow, type GameTab } from "../components/game/GameUi";
import HeroPortrait from "../components/heroes/HeroPortrait";
import HeroGearTab from "../components/heroes/screen/HeroGearTab";
import HeroSkillsTab from "../components/heroes/screen/HeroSkillsTab";
import HeroStatsTab from "../components/heroes/screen/HeroStatsTab";
import { useSession } from "../hooks/useSession";
import { sortRoster } from "../lib/heroOrder";
import { rankLabel, useCatalog } from "../lib/queries/catalog";
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
} from "../lib/queries/heroes";
import { useEquip, useEquipBest, useInventory, useUnequip, useUnequipAll } from "../lib/queries/inventory";

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
  const appointLeader = useAppointLeader(playerId);
  const heal = useHealHero(playerId);
  const resetLevel = useResetHeroLevel(playerId);
  const upgradeSkill = useUpgradeHeroSkill(playerId);
  const equip = useEquip(playerId);
  const unequip = useUnequip(playerId);
  const unequipAll = useUnequipAll(playerId);
  const equipBest = useEquipBest(playerId);

  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as Tab | null) ?? "stats";

  if (heroes.isPending) {
    return <p className="text-slate-500">Завантаження героя…</p>;
  }

  if (heroes.isError) {
    return <ErrorBanner error={heroes.error} />;
  }

  const roster = sortRoster(heroes.data.heroes, (key) => catalog.hero(key)?.rank);
  const position = roster.findIndex((hero) => hero.id === heroId);
  const hero = roster[position];
  const config = hero === undefined ? null : catalog.hero(hero.heroKey);

  if (hero === undefined) {
    return <Navigate to="/heroes" replace />;
  }

  const mutations = [levelUp, advanceStar, convertShards, evolve, appointLeader, heal, resetLevel, upgradeSkill, equip, unequip, unequipAll, equipBest];
  const busy = mutations.some((mutation) => mutation.isPending);
  const failure = mutations.map((mutation) => mutation.error).find((error) => error !== null) ?? null;

  const go = (step: number) => {
    const next = roster[(position + step + roster.length) % roster.length];
    if (next !== undefined) void navigate(`/heroes/${next.id}?tab=${tab}`, { replace: true });
  };

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
  const affordable = heroes.data.experience >= hero.experienceToNext;
  const starCost = hero.nextStarPartCost ?? null;
  const shardsMissing = starCost === null ? 0 : Math.max(0, starCost - hero.shards);

  const tabs: GameTab<Tab>[] = [
    { key: "stats", label: "Показники" },
    { key: "skills", label: "Навички" },
    { key: "gear", label: "Спорядж." },
  ];

  return (
    <div className="space-y-3">
      <GameScreen
        title={
          <span className="flex items-center gap-2">
            <Link to="/heroes" className="text-2xl leading-none text-sky-200 hover:text-white" aria-label="До ростеру">
              ←
            </Link>
            {catalog.heroName(hero.heroKey)}
            <span className="rounded bg-amber-400 px-1.5 text-xs font-bold text-amber-950">T{hero.tier}</span>
          </span>
        }
        actions={
          <Link to="/heroes/codex" className="text-sm text-sky-200 hover:underline">
            кодекс
          </Link>
        }
      >
        <div className="relative flex flex-col items-center gap-3 pb-2">
          <div className="flex items-center gap-2">
            <span className={`text-3xl font-black italic drop-shadow ${RANK_COLORS[rank] ?? ""}`}>{rankLabel(rank)}</span>
            <RoleBadge role={config?.class} size={28} />
          </div>

          <div className="flex w-full items-center justify-between">
            <button type="button" onClick={() => go(-1)} className="px-3 text-4xl text-sky-200 hover:text-white" aria-label="Попередній герой">
              ‹
            </button>
            <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={200} />
            <button type="button" onClick={() => go(1)} className="px-3 text-4xl text-sky-200 hover:text-white" aria-label="Наступний герой">
              ›
            </button>
          </div>

          <p className="rounded-full bg-gradient-to-r from-transparent via-rose-700/70 to-transparent px-8 py-0.5 text-lg font-bold">
            ⚔ {Math.round(hero.power).toLocaleString("uk-UA")}
          </p>

          {/* Зірки за осколки героя (GDD §6.1): кожна частинка — +5% бойової міці */}
          <div className="flex items-center gap-4">
            <StarRow starParts={hero.starParts} partsPerStar={catalog.partsPerStar} maxStars={catalog.maxStars} />
            <GameButton
              onClick={() => advanceStar.mutate(hero.id)}
              disabled={busy || starCost === null || shardsMissing > 0}
              title={starCost === null ? "Усі зірки заповнені" : `Частинка зірки · ${starCost} осколків (є ${hero.shards})`}
            >
              ⬆
            </GameButton>
          </div>
          {shardsMissing > 0 && (
            <p className="text-xs text-sky-100/80">
              Для частинки зірки бракує {shardsMissing} осколків героя
              {universal > 0 && (
                <>
                  {" · "}
                  <button
                    type="button"
                    onClick={() => convertShards.mutate({ heroKey: hero.heroKey, count: Math.min(shardsMissing, universal) })}
                    disabled={busy}
                    className="text-emerald-300 underline disabled:opacity-50"
                  >
                    добрати {Math.min(shardsMissing, universal)} з універсальних
                  </button>
                </>
              )}
            </p>
          )}

          <div className="grid w-full max-w-md grid-cols-3 divide-x divide-[#335c9a] rounded-xl bg-[#16305e] py-2 text-center ring-1 ring-[#335c9a]">
            <div>
              <p className="text-xs text-sky-200/80">Конвої</p>
              <p className="font-bold">{hero.convoys}</p>
            </div>
            <div>
              <p className="text-xs text-sky-200/80">Рівень</p>
              <p className="text-2xl font-black">{hero.effectiveLevel}</p>
            </div>
            <div>
              <p className="text-xs text-sky-200/80">Місткість війська</p>
              <p className="font-bold">{hero.convoyCapacity.toLocaleString("uk-UA")}</p>
            </div>
          </div>

          {atLevelCap ? (
            <p className="text-sm text-sky-100/80">Максимальний рівень!</p>
          ) : (
            <div className="flex flex-col items-center gap-1">
              <GameButton onClick={() => levelUp.mutate({ heroId: hero.id, levels: 1 })} disabled={busy || !affordable} variant="accent">
                Підняти рівень · {hero.experienceToNext.toLocaleString("uk-UA")} досвіду
              </GameButton>
              {!affordable && (
                <p className="text-xs text-sky-100/70">
                  Бракує {(hero.experienceToNext - heroes.data.experience).toLocaleString("uk-UA")} досвіду — його дають баночки досвіду.
                </p>
              )}
            </div>
          )}
        </div>

        <GameTabs tabs={tabs} value={tab} onChange={(next) => setParams({ tab: next }, { replace: true })} />
        <GamePanel>
          {config === null ? (
            <p className="text-sm text-sky-100/80">Героя немає в довіднику.</p>
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
              hero={hero}
              config={config}
              equipment={inventory.data?.equipment ?? []}
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
              onAppointLeader={() => appointLeader.mutate(hero.id)}
              onHeal={() => heal.mutate(hero.id)}
              onResetLevel={() => resetLevel.mutate(hero.id)}
            />
          )}
        </GamePanel>
      </GameScreen>

      <ErrorBanner error={failure} />

      {equipBest.data?.equipped === 0 && tab === "gear" && (
        <p className="text-sm text-slate-600">Кращого вільного спорядження немає — усе найкраще вже вдягнене.</p>
      )}
    </div>
  );
}
