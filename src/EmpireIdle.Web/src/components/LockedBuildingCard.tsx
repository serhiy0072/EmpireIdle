import type { BuildingResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";

interface Props {
  building: BuildingResponse;
}

/** Будівля під туманом війни (GDD §3.1): силует і умова відкриття, без взаємодії. */
export default function LockedBuildingCard({ building }: Props) {
  const catalog = useCatalog();
  const gateLevel = catalog.building(building.type)?.requiresMainBuildingLevel ?? 0;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 space-y-1">
      <h3 className="font-medium text-slate-500">🔒 {catalog.buildingName(building.type)}</h3>
      <p className="text-sm text-slate-500">Відкриється, коли ратуша досягне рівня {gateLevel}.</p>
    </div>
  );
}
