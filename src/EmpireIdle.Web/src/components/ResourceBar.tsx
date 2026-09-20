import type { VillageResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";

interface Props {
  village: VillageResponse | undefined;
}

export default function ResourceBar({ village }: Props) {
  const catalog = useCatalog();

  if (village === undefined) {
    return <div className="h-6 w-64 animate-pulse rounded bg-slate-200" />;
  }

  return (
    <div className="flex flex-wrap items-center gap-3">
      {village.resources.map((resource) => (
        <span key={resource.resourceType} className="rounded-full bg-slate-100 px-3 py-1 text-sm text-slate-700">
          {catalog.resourceName(resource.resourceType)}:{" "}
          <span className="font-medium">{resource.amount.toLocaleString("uk-UA")}</span>
        </span>
      ))}
    </div>
  );
}
