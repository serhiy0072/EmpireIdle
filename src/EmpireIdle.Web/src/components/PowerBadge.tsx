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
      className="flex items-center gap-2 rounded-full bg-gradient-to-b from-amber-400 to-orange-600 px-3 py-0.5 text-sm font-semibold text-white ring-1 ring-amber-200 [text-shadow:0_1px_1px_#0006] hover:brightness-110"
    >
      <span>
        🏰 <span className="font-medium">{mainLevel ?? "…"}</span>
      </span>
      <span className="text-amber-100/70">|</span>
      <span>
        ⚔ <span className="font-medium">{total === undefined ? "…" : compactPower(total)}</span>
      </span>
    </Link>
  );
}
