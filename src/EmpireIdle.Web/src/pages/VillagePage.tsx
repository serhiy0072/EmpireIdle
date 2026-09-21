import { useState } from "react";
import BuildingCard from "../components/BuildingCard";
import ErrorBanner from "../components/ErrorBanner";
import VillageMap from "../components/village/VillageMap";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import { useCollectBuilding, useSpeedUpBuilding, useUpgradeBuilding, useVillage } from "../lib/queries/village";

export default function VillagePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const village = useVillage(playerId);
  const collect = useCollectBuilding(playerId);
  const upgrade = useUpgradeBuilding(playerId);
  const speedUp = useSpeedUpBuilding(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження села…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const busy = collect.isPending || upgrade.isPending || speedUp.isPending;
  const failure = collect.error ?? upgrade.error ?? speedUp.error;
  const selected = village.data.buildings.find((building) => building.id === selectedId) ?? null;

  return (
    <div className="flex h-[calc(100vh-9rem)] flex-col gap-3">
      <h1 className="text-xl font-medium text-slate-800">{village.data.name}</h1>

      <ErrorBanner error={failure} />

      <div className="relative min-h-0 flex-1">
        <VillageMap
          buildings={village.data.buildings}
          catalog={catalog}
          selectedId={selectedId}
          onSelect={setSelectedId}
          onCollect={(buildingId) => collect.mutate(buildingId)}
        />

        {selected !== null && (
          <div className="absolute inset-x-3 bottom-3 max-w-sm">
            <BuildingCard
              building={selected}
              busy={busy}
              onCollect={() => collect.mutate(selected.id)}
              onUpgrade={() => upgrade.mutate(selected.id)}
              onSpeedUp={() => speedUp.mutate(selected.id)}
            />
          </div>
        )}
      </div>
    </div>
  );
}
