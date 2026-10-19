import { useState } from "react";
import BuildingCard from "../components/BuildingCard";
import HungerBanner from "../components/HungerBanner";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton } from "../components/game/GameUi";
import LockedBuildingCard from "../components/LockedBuildingCard";
import DamageBanner from "../components/village/DamageBanner";
import VillageMap from "../components/village/VillageMap";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import {
  useMoveBuilding,
  useRepairVillage,
  useSpeedUpBuilding,
  useUpgradeBuilding,
  useVillage,
} from "../lib/queries/village";

type VillageAction = "upgrade" | "speedUp" | "repair" | "move";

export default function VillagePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const village = useVillage(playerId);
  const upgrade = useUpgradeBuilding(playerId);
  const speedUp = useSpeedUpBuilding(playerId);
  const repair = useRepairVillage(playerId);
  const move = useMoveBuilding(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  // Режим «Перебудувати»: будівлі тягнуться мишею по сітці (рішення 13.10.2026)
  const [editing, setEditing] = useState(false);
  // Банер належить останній дії: помилка давнього кліку після успішного нового лише плутає
  const [lastAction, setLastAction] = useState<VillageAction | null>(null);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження села…</p>;
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

  return (
    <div className="relative h-full">
      <VillageMap
        buildings={village.data.buildings}
        catalog={catalog}
        selectedId={selectedId}
        onSelect={setSelectedId}
        editing={editing}
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
              setSelectedId(null);
            }}
            variant={editing ? "accent" : "secondary"}
          >
            {editing ? "Готово" : "Перебудувати"}
          </GameButton>
        </div>
      </div>

      <div className="absolute inset-x-3 top-16 mx-auto max-w-xl space-y-2 empty:hidden">
        <ErrorBanner error={failure} />

        {village.data.isHungry && <HungerBanner />}

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
          Перетягніть будівлю на вільне місце всередині муру
        </p>
      )}

      {selected !== null && !editing && (
        <div className="absolute inset-x-3 bottom-3 max-w-sm">
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
