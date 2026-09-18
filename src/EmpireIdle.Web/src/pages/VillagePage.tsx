import BuildingCard from "../components/BuildingCard";
import { useSession } from "../hooks/useSession";
import { describeError } from "../lib/errorMessages";
import { useCollectBuilding, useSpeedUpBuilding, useUpgradeBuilding, useVillage } from "../lib/queries/village";

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
    return <p className="text-red-600">{describeError(village.error)}</p>;
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-medium text-slate-800">{village.data.name}</h1>

      {failure !== null && failure !== undefined && (
        <p className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{describeError(failure)}</p>
      )}

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
