import type { VillageResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { compactPower } from "../lib/queries/rating";
import { ResourceGlyph } from "./shell/icons";

interface Props {
  village: VillageResponse | undefined;
}

/** Ресурси в шапці: знак і компактне число, повне — у підказці. */
export default function ResourceBar({ village }: Props) {
  const catalog = useCatalog();

  if (village === undefined) {
    return <div className="h-7 w-64 animate-pulse rounded-full bg-white/10" />;
  }

  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {village.resources
        .filter((resource) => resource.isUnlocked)
        .sort((a, b) => catalog.resourceOrder(a.resourceType) - catalog.resourceOrder(b.resourceType))
        .map((resource) => (
          <span
            key={resource.resourceType}
            title={`${catalog.resourceName(resource.resourceType)}: ${resource.amount.toLocaleString("uk-UA")}`}
            className="flex items-center gap-1 rounded-full bg-black/30 py-0.5 pl-0.5 pr-3 text-sm font-semibold tabular-nums text-white ring-1 ring-white/15"
          >
            <ResourceGlyph resource={resource.resourceType} />
            {compactPower(resource.amount)}
          </span>
        ))}
    </div>
  );
}
