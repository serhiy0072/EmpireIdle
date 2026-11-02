import { useState } from "react";
import { Link, Navigate, useNavigate, useParams } from "react-router-dom";
import { LevelStrip, OrderCard, TrainForm, UnitStats, UpgradeForm } from "../components/army/CampPanels";
import { UnitFigure } from "../components/army/UnitArt";
import ErrorBanner from "../components/ErrorBanner";
import { GameTabs } from "../components/game/GameUi";
import { useSession } from "../hooks/useSession";
import { cumulativeUnitLevelCost } from "../lib/progression";
import type { CatalogUnit } from "../lib/queries/catalog";
import { useCatalog } from "../lib/queries/catalog";
import {
  useGarrison,
  useLevelUpUnits,
  useSpeedUpLevelUp,
  useSpeedUpTraining,
  useTrainUnits,
  useUnitLevelCeiling,
} from "../lib/queries/garrison";
import { useVillage } from "../lib/queries/village";
import { roman, tierFrame } from "../lib/unitTiers";
import { formatNumber } from "../lib/format";
import { t } from "i18next";

type Mode = "train" | "stats" | "upgrade";

/**
 * Табір юнітів (референс 13.10.2026): одна будівля — один табір. Сцена з юнітом обраного рівня,
 * смуга рівнів I…X із кількістю, під нею — черга будівлі або нова партія; вкладки внизу перемикають табори.
 */
export default function UnitCampPage() {
  const { building = "" } = useParams();
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const village = useVillage(playerId);
  const garrison = useGarrison(playerId);
  const navigate = useNavigate();

  if (catalog.loaded && !catalog.trainingBuildingKeys.includes(building)) {
    return <Navigate to="/army" replace />;
  }

  if (village.isPending || garrison.isPending) {
    return <p className="text-slate-500">{t("army:unitCamp.loading")}</p>;
  }

  if (village.isError) return <ErrorBanner error={village.error} />;
  if (garrison.isError) return <ErrorBanner error={garrison.error} />;

  // Табори — будівлі села, що тренують: порядок і назви з довідника
  const camps = catalog.trainingBuildingKeys
    .map((key) => village.data.buildings.find((candidate) => candidate.type === key && candidate.isUnlocked))
    .filter((candidate) => candidate !== undefined);
  const camp = camps.find((candidate) => candidate.type === building);
  const unit = camp === undefined ? undefined : catalog.unitsFor(camp.type, camp.level)[0];

  return (
    <div className="mx-auto flex w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-gradient-to-b from-[#7fa6d6] via-[#3b6aa8] to-[#1d3b6f] text-white shadow-lg">
      {camp === undefined || unit === undefined ? (
        <div className="space-y-3 p-6 text-center">
          <p>{t("army:unitCamp.notBuilt", { building: catalog.buildingName(building) })}</p>
          <Link to="/" className="text-sky-100 underline">
            {t("army:unitCamp.toVillage")}
          </Link>
        </div>
      ) : (
        // key — інший табір починає з власного вибору рівня
        <Camp
          key={camp.type}
          playerId={playerId}
          buildingType={camp.type}
          buildingLevel={camp.level}
          underConstruction={camp.isUnderConstruction}
          unit={unit}
        />
      )}

      {camps.length > 1 && (
        <div className="bg-[#16305e] px-2 pt-2">
          <GameTabs
            tabs={camps.map((candidate) => ({ key: candidate.type, label: catalog.buildingName(candidate.type) }))}
            value={building}
            onChange={(next) => navigate(`/army/${next}`, { replace: true })}
          />
        </div>
      )}
    </div>
  );
}

interface CampProps {
  playerId: string;
  buildingType: string;
  buildingLevel: number;
  underConstruction: boolean;
  unit: CatalogUnit;
}

function Camp({ playerId, buildingType, buildingLevel, underConstruction, unit }: CampProps) {
  const catalog = useCatalog();
  const garrison = useGarrison(playerId);
  const ceiling = useUnitLevelCeiling(playerId);
  const train = useTrainUnits(playerId);
  const levelUp = useLevelUpUnits(playerId);
  const speedUpTraining = useSpeedUpTraining(playerId);
  const speedUpLevelUp = useSpeedUpLevelUp(playerId);

  const counts: Record<number, number> = {};
  for (const stack of garrison.data?.units ?? []) {
    if (stack.unitType === unit.key) counts[stack.level] = (counts[stack.level] ?? 0) + stack.count;
  }

  const owned = Object.keys(counts).map(Number).filter((level) => (counts[level] ?? 0) > 0);
  const [selected, setSelected] = useState(() => (owned.length > 0 ? Math.max(...owned) : ceiling));
  const [mode, setMode] = useState<Mode>("train");

  // У кожної будівлі своя черга тренування й своя черга прокачки (рішення 12.10.2026)
  const ownUnits = new Set(catalog.unitsFor(buildingType, buildingLevel).map((candidate) => candidate.key));
  const training = garrison.data?.trainingOrders.find((order) => ownUnits.has(order.unitType));
  const levelling = garrison.data?.levelUpOrders.find((order) => ownUnits.has(order.unitType));
  // Місткість своя в кожної будівлі: рахуються її типи в гарнізоні, на тренуванні й на прокачці (рішення 13.10.2026)
  const capacity = catalog.unitCapacity(buildingLevel);
  const occupied =
    (garrison.data?.units ?? []).filter((stack) => ownUnits.has(stack.unitType)).reduce((sum, stack) => sum + stack.count, 0) +
    (training?.count ?? 0) +
    (levelling?.count ?? 0);
  const free = Math.max(0, capacity - occupied);
  const maxBatch = catalog.unitBatchSize(buildingLevel);
  const frame = tierFrame(selected);

  const locked = selected > ceiling ? `${t("army:camp.levelLocked", { level: roman(selected), townHall: selected })}.` : null;
  const sources = Object.entries(counts)
    .map(([level, count]) => [Number(level), count] as [number, number])
    .filter(([level, count]) => level < selected && count > 0)
    .sort(([a], [b]) => a - b);

  const toggle = (next: Mode) => setMode((current) => (current === next ? "train" : next));
  const sideButton =
    "flex h-10 w-12 items-center justify-center rounded-lg bg-slate-100/80 text-xl text-slate-600 shadow ring-1 ring-white hover:bg-white";

  return (
    <>
      <header className="flex items-center gap-3 px-3 pt-3">
        <Link to="/army" aria-label={t("army:unitCamp.toArmy")} className="text-3xl font-bold text-white drop-shadow hover:text-sky-100">
          ←
        </Link>
        <h1 className="flex-1 text-center text-xl font-semibold drop-shadow">
          {unit.displayName} {roman(selected)}
        </h1>
        <span
          title={t("army:unitCamp.buildingLevel", { building: catalog.buildingName(buildingType), level: buildingLevel })}
          className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-b from-rose-500 to-red-700 text-sm font-bold ring-2 ring-amber-300"
        >
          {buildingLevel}
        </span>
      </header>

      <div className="relative flex h-64 items-end justify-center">
        <span
          aria-hidden
          className="absolute bottom-2 h-10 w-56 rounded-[50%] opacity-60 blur-md"
          style={{ background: `radial-gradient(${frame.from}, transparent 70%)` }}
        />
        <span className="relative">
          <UnitFigure unitType={unit.key} level={selected} size={190} />
        </span>
        <div className="absolute bottom-4 right-3 flex flex-col gap-2">
          <button type="button" aria-label={t("army:camp.stats")} title={t("army:camp.stats")} onClick={() => toggle("stats")} className={sideButton}>
            {mode === "stats" ? "↶" : "☰"}
          </button>
          <button type="button" aria-label={t("army:unitCamp.levelUpHere")} title={t("army:unitCamp.levelUpHere")} onClick={() => toggle("upgrade")} className={sideButton}>
            {mode === "upgrade" ? "↶" : "⇧"}
          </button>
        </div>
      </div>

      <LevelStrip unitType={unit.key} maxLevel={catalog.maxUnitLevel} ceiling={ceiling} counts={counts} selected={selected} onSelect={setSelected} />

      <p className="px-3 text-right text-xs text-sky-100 [text-shadow:0_1px_2px_#000]">
        {t("army:unitCamp.capacity", { occupied: formatNumber(occupied), capacity: formatNumber(capacity) })}
      </p>

      <div className="space-y-3 bg-[#1d3b6f]/80 p-3">
        {mode === "stats" && <UnitStats unit={unit} level={selected} maxLevel={catalog.maxUnitLevel} />}

        {mode === "train" &&
          (training !== undefined ? (
            <OrderCard
              title={t("army:unitCamp.trainingTitle")}
              description={t("army:unitCamp.trainingDescription", {
                unit: catalog.unitName(training.unitType),
                level: roman(training.level),
                count: formatNumber(training.count),
              })}
              unitType={training.unitType}
              level={training.level}
              totalSeconds={cumulativeUnitLevelCost(unit.baseTrainSeconds, 1, training.level + 1, unit.trainTimeGrowth) * training.count}
              orderId={training.id}
              completesAt={training.completesAt}
              speedUpCostGems={training.speedUpCostGems}
              timer="Training"
              onFinish={() => speedUpTraining.mutate(training.id)}
              finishing={speedUpTraining.isPending}
              error={speedUpTraining.error}
            />
          ) : (
            <TrainForm
              key={selected}
              unit={unit}
              level={selected}
              maxBatch={Math.max(1, Math.min(maxBatch, free))}
              blocked={
                locked ??
                (underConstruction
                  ? t("army:unitCamp.underConstruction")
                  : free === 0
                    ? t("army:unitCamp.full")
                    : null)
              }
              pending={train.isPending}
              error={train.error}
              onTrain={(count) => train.mutate({ unitType: unit.key, level: selected, count })}
            />
          ))}

        {mode === "upgrade" &&
          (levelling !== undefined ? (
            <OrderCard
              title={t("army:unitCamp.levellingTitle")}
              description={t("army:unitCamp.levellingDescription", {
                unit: catalog.unitName(levelling.unitType),
                from: roman(levelling.fromLevel),
                to: roman(levelling.toLevel),
                count: formatNumber(levelling.count),
              })}
              unitType={levelling.unitType}
              level={levelling.toLevel}
              totalSeconds={
                cumulativeUnitLevelCost(unit.baseTrainSeconds, levelling.fromLevel, levelling.toLevel, unit.trainTimeGrowth) * levelling.count
              }
              orderId={levelling.id}
              completesAt={levelling.completesAt}
              speedUpCostGems={levelling.speedUpCostGems}
              timer="UnitLevelUp"
              onFinish={() => speedUpLevelUp.mutate(levelling.id)}
              finishing={speedUpLevelUp.isPending}
              error={speedUpLevelUp.error}
            />
          ) : (
            <UpgradeForm
              key={selected}
              unit={unit}
              toLevel={selected}
              sources={sources}
              maxBatch={maxBatch}
              blocked={locked}
              pending={levelUp.isPending}
              error={levelUp.error}
              onUpgrade={(fromLevel, count) => levelUp.mutate({ unitType: unit.key, fromLevel, toLevel: selected, count })}
            />
          ))}

        {/* Прокачка йде паралельно з тренуванням — нагадуємо про неї, поки гравець дивиться інше */}
        {mode !== "upgrade" && levelling !== undefined && (
          <button type="button" onClick={() => setMode("upgrade")} className="w-full text-center text-xs text-sky-200 underline">
            {t("army:unitCamp.levellingNow", {
              unit: catalog.unitName(levelling.unitType),
              from: roman(levelling.fromLevel),
              to: roman(levelling.toLevel),
              count: levelling.count,
            })}
          </button>
        )}
      </div>
    </>
  );
}
