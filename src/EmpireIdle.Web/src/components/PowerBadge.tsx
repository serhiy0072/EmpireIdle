import { Link } from "react-router-dom";
import type { VillageResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { compactPower, usePower } from "../lib/queries/rating";

interface Props {
  playerId: string;
  village: VillageResponse | undefined;
}

/**
 * Рівень і сила в шапці — «прогрес» одним поглядом, як досвід у RPG:
 * рівень ратуші відкриває будівлі, сила вирішує бої. Клік веде в рейтинг.
 */
export default function PowerBadge({ playerId, village }: Props) {
  const catalog = useCatalog();
  const power = usePower(playerId);

  const mainLevel = village?.buildings.find((building) => building.type === catalog.mainBuildingKey)?.level;
  const total = power.data?.total;
  const breakdown =
    power.data === undefined
      ? "Сила рахується…"
      : `Армія ${compactPower(power.data.army)} · герої ${compactPower(power.data.hero)} · спорядження ${compactPower(power.data.equipment)}`;

  return (
    <Link
      to="/rating"
      title={breakdown}
      className="flex items-center gap-2 rounded-full bg-amber-50 px-3 py-1 text-sm text-amber-900 hover:bg-amber-100"
    >
      <span>
        🏰 <span className="font-medium">{mainLevel ?? "…"}</span>
      </span>
      <span className="text-amber-300">|</span>
      <span>
        ⚔ <span className="font-medium">{total === undefined ? "…" : compactPower(total)}</span>
      </span>
    </Link>
  );
}
