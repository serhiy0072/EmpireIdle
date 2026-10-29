import { useState } from "react";
import { useNavigate } from "react-router-dom";
import BuildingCard from "../components/BuildingCard";
import HungerBanner from "../components/HungerBanner";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton } from "../components/game/GameUi";
import LockedBuildingCard from "../components/LockedBuildingCard";
import type { RingAction } from "../components/village/BuildingActionRing";
import DamageBanner from "../components/village/DamageBanner";
import VillageMap from "../components/village/VillageMap";
import { useHotkeys } from "../hooks/useHotkeys";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { BuildingResponse } from "../lib/apiTypes";
import { screensFor } from "../lib/buildingScreens";
import { useCatalog } from "../lib/queries/catalog";
import {
  useMoveBuilding,
  useRepairVillage,
  useSpeedUpBuilding,
  useUpgradeBuilding,
  useVillage,
} from "../lib/queries/village";
import { speedUpLabel } from "../lib/speedUp";
import { t } from "i18next";

type VillageAction = "upgrade" | "speedUp" | "repair" | "move";

/** Стіна — периметр без місця на плані: кільця під нею немає, одразу картка. */
const WALL_KEY = "wall";

export default function VillagePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const navigate = useNavigate();
  const now = useNow();

  const village = useVillage(playerId);
  const upgrade = useUpgradeBuilding(playerId);
  const speedUp = useSpeedUpBuilding(playerId);
  const repair = useRepairVillage(playerId);
  const move = useMoveBuilding(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Картка будівлі — за кнопкою «Подробиці» в кільці, а не на кожен клік (референс 29.10.2026)
  const [details, setDetails] = useState(false);
  // Режим «Перебудувати»: будівлі тягнуться мишею по сітці (рішення 13.10.2026)
  const [editing, setEditing] = useState(false);
  // Банер належить останній дії: помилка давнього кліку після успішного нового лише плутає
  const [lastAction, setLastAction] = useState<VillageAction | null>(null);

  const select = (buildingId: string | null) => {
    setSelectedId(buildingId);
    setDetails(false);
  };

  // Esc у селі не веде назад: закриває картку, знімає вибір будівлі, а потім виходить з «Перебудувати»
  useHotkeys(
    { Escape: () => (details ? setDetails(false) : selectedId !== null ? select(null) : setEditing(false)) },
    { enabled: selectedId !== null || editing },
  );

  if (village.isPending) {
    return <p className="text-slate-500">{t("village:page.loading")}</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const busy = upgrade.isPending || speedUp.isPending || repair.isPending;
  const actions = { upgrade, speedUp, repair, move };
  const failure = lastAction === null ? null : actions[lastAction].error;

  const run = (action: VillageAction, perform: () => void) => {
    setLastAction(action);
    perform();
  };

  const selected = village.data.buildings.find((building) => building.id === selectedId) ?? null;

  /** Кнопки кільця: подробиці, покращення або прискорення, переходи на екрани будівлі. */
  const ringActions = (building: BuildingResponse): RingAction[] => {
    const detailsAction: RingAction = { key: "details", icon: "📄", label: t("village:ring.details"), onClick: () => setDetails(true) };

    if (!building.isUnlocked) return [detailsAction];

    const upgradable = catalog.building(building.type)?.upgradable !== false;
    const completesAt = building.isUnderConstruction ? building.constructionCompletesAt : null;
    const gems =
      completesAt == null || building.speedUpCostGems == null
        ? null
        : catalog.speedUpCost("Construction", completesAt, now, building.speedUpCostGems);

    const timer: RingAction[] =
      completesAt == null
        ? upgradable
          ? [
              {
                key: "upgrade",
                icon: "⬆️",
                label: t("village:card.upgrade"),
                disabled: busy,
                tutorial: "upgrade",
                onClick: () => run("upgrade", () => upgrade.mutate(building.id)),
              },
            ]
          : []
        : gems === 0
          ? []
          : [
              {
                key: "speedUp",
                icon: "⚡",
                label: t("village:ring.speedUp"),
                note: gems === null ? undefined : speedUpLabel(gems),
                accent: true,
                disabled: busy,
                onClick: () => run("speedUp", () => speedUp.mutate(building.id)),
              },
            ];

    const screens = screensFor(building.type).map<RingAction>((screen) => ({
      key: screen.to,
      icon: screen.icon,
      label: screen.label,
      onClick: () => void navigate(screen.to),
    }));

    return [detailsAction, ...timer, ...screens];
  };

  const showCard = selected !== null && !editing && (details || selected.type === WALL_KEY);

  return (
    <div className="relative h-full">
      <VillageMap
        buildings={village.data.buildings}
        catalog={catalog}
        selectedId={selectedId}
        onSelect={select}
        editing={editing}
        actions={selected === null || editing ? [] : ringActions(selected)}
        onMove={(buildingId, x, y) => run("move", () => move.mutate({ buildingId, x, y }))}
      />

      {/* Табличка села й дії — поверх поля, як у HUD гри */}
      <div className="pointer-events-none absolute inset-x-3 top-3 flex flex-wrap items-start gap-2">
        <h1 className="rounded-xl bg-gradient-to-b from-[#24467f] to-[#16305e] px-4 py-1.5 text-base font-semibold text-white shadow-lg ring-2 ring-[#5b85c4] [text-shadow:0_1px_1px_#0008]">
          {village.data.name}
        </h1>
        <div className="flex-1" />
        <div className="pointer-events-auto flex gap-2">
          <GameButton
            onClick={() => {
              setEditing(!editing);
              select(null);
            }}
            variant={editing ? "accent" : "secondary"}
          >
            {editing ? t("village:page.done") : t("village:page.rearrange")}
          </GameButton>
        </div>
      </div>

      <div className="absolute inset-x-3 top-16 mx-auto max-w-xl space-y-2 empty:hidden">
        <ErrorBanner error={failure} />

        {village.data.isHungry && <HungerBanner kind="village" />}

        {village.data.damage != null && (
          <DamageBanner
            damage={village.data.damage}
            damagedNames={village.data.buildings
              .filter((building) => building.damageLevel > 0)
              .map((building) => catalog.buildingName(building.type))}
            resourceName={catalog.resourceName}
            busy={busy}
            onRepair={() => run("repair", () => repair.mutate())}
          />
        )}
      </div>

      {editing && (
        <p className="pointer-events-none absolute inset-x-0 bottom-3 mx-auto w-fit rounded-lg bg-amber-100/95 px-3 py-1 text-sm text-amber-900 shadow">
          {t("village:page.dragHint")}
        </p>
      )}

      {showCard && (
        // Над смужкою чату, щоб вона не перекривала картку
        <div className="absolute inset-x-3 bottom-20 max-w-sm">
          {selected.isUnlocked ? (
            <BuildingCard
              playerId={playerId}
              building={selected}
              busy={busy}
              onUpgrade={() => run("upgrade", () => upgrade.mutate(selected.id))}
              onSpeedUp={() => run("speedUp", () => speedUp.mutate(selected.id))}
            />
          ) : (
            <LockedBuildingCard building={selected} />
          )}
        </div>
      )}
    </div>
  );
}
