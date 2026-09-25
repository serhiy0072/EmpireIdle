import { useNow } from "../../hooks/useNow";
import type { MarchResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_STATE, MARCH_TARGET, useSpeedUpMarch } from "../../lib/queries/marches";
import { speedUpLabel } from "../../lib/speedUp";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
  marches: MarchResponse[];
}

/** Активні походи: куди, скільки, коли прибуде, і прискорення за gems. */
export default function MarchList({ playerId, marches }: Props) {
  const now = useNow();
  const catalog = useCatalog();
  const speedUp = useSpeedUpMarch(playerId);

  if (marches.length === 0) {
    return <p className="text-sm text-slate-500">Армія вдома — походів немає.</p>;
  }

  return (
    <div className="space-y-2">
      <ErrorBanner error={speedUp.error} />

      {marches.map((march) => {
        const returning = march.state === MARCH_STATE.returning;
        const total = march.units.reduce((sum, unit) => sum + unit.count, 0);
        const level = march.targetLevel != null ? ` (рів. ${march.targetLevel})` : "";
        const place = march.targetName != null ? `${march.targetName}${level}` : `(${march.targetX}, ${march.targetY})`;
        const target = march.targetType === MARCH_TARGET.clanStructure ? `споруду клану ${place}` : place;

        return (
          <div key={march.id} className="flex items-center justify-between gap-3 rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <div>
              <p className="text-slate-800">
                {returning ? "Повертається з" : "Іде на"} {target}
              </p>
              <p className="text-xs text-slate-500">
                {march.units
                  .map((unit) => `${catalog.unitName(unit.unitType)} ×${unit.count}`)
                  .join(", ")}{" "}
                · разом {total.toLocaleString("uk-UA")}
              </p>
            </div>

            <div className="flex items-center gap-2">
              <span className="font-mono text-slate-700">{formatRemaining(march.arrivesAt, now)}</span>
              {catalog.speedUpCost(march.arrivesAt, now, march.speedUpCostGems) > 0 && (
                <button
                  type="button"
                  onClick={() => speedUp.mutate(march.id)}
                  disabled={speedUp.isPending}
                  className="rounded-lg border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-white disabled:opacity-50"
                >
                  {speedUpLabel(catalog.speedUpCost(march.arrivesAt, now, march.speedUpCostGems))}
                </button>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
