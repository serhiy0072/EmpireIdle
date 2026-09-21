import BuildingCard from "../components/BuildingCard";
import { useSession } from "../hooks/useSession";
import { useCollectBuilding, useSpeedUpBuilding, useUpgradeBuilding, useVillage } from "../lib/queries/village";
import ErrorBanner from "../components/ErrorBanner";

export default function VillagePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const village = useVillage(playerId);
  const collect = useCollectBuilding(playerId);
  const upgrade = useUpgradeBuilding(playerId);
  const speedUp = useSpeedUpBuilding(playerId);

  const busy = collect.isPending || upgrade.isPending || speedUp.isPending;
  const failure = collect.error ?? upgrade.error ?? speedUp.error ?? village.error;

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження села…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-medium text-slate-800">{village.data.name}</h1>

      <ErrorBanner error={failure} />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {village.data.buildings.map((building) => (
          <BuildingCard
            key={building.id}
            building={building}
            busy={busy}
            onCollect={() => collect.mutate(building.id)}
            onUpgrade={() => upgrade.mutate(building.id)}
            onSpeedUp={() => speedUp.mutate(building.id)}
          />
        ))}
      </div>
    </div>
  );
}
