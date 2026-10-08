import { useNow } from "../../hooks/useNow";
import type { MarchResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_INTENT, MARCH_STATE, MARCH_TARGET, useRecallCamp, useSpeedUpMarch } from "../../lib/queries/marches";
import { speedUpLabel } from "../../lib/speedUp";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
  marches: MarchResponse[];
}

/** Активні походи: куди, скільки, коли прибуде, і прискорення за gems; табори — з відкликанням. */
export default function MarchList({ playerId, marches }: Props) {
  const now = useNow();
  const catalog = useCatalog();
  const speedUp = useSpeedUpMarch(playerId);
  const recall = useRecallCamp(playerId);

  if (marches.length === 0) {
    return <p className="text-sm text-slate-500">Армія вдома — походів немає.</p>;
  }

  return (
    <div className="space-y-2">
      <ErrorBanner error={speedUp.error ?? recall.error} />

      {marches.map((march) => {
        const returning = march.state === MARCH_STATE.returning;
        const camping = march.state === MARCH_STATE.camping;
        const total = march.units.reduce((sum, unit) => sum + unit.count, 0);
        const level = march.targetLevel != null ? ` (рів. ${march.targetLevel})` : "";
        const place = march.targetName != null ? `${march.targetName}${level}` : `(${march.targetX}, ${march.targetY})`;
        const target = march.targetType === MARCH_TARGET.clanStructure ? `споруду клану ${place}` : place;

        return (
          <div key={march.id} className="flex items-center justify-between gap-3 rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <div>
              <p className="text-slate-800">
                {camping ? (
                  <>
                    Табір на ({march.targetX}, {march.targetY}) — {target} переїхало
                  </>
                ) : (
                  <>
                    {returning ? "Повертається з" : march.intent === MARCH_INTENT.scout ? "Розвідники йдуть на" : "Іде на"}{" "}
                    {target}
                  </>
                )}
              </p>
              <p className="text-xs text-slate-500">
                {march.units
                  .map((unit) => `${catalog.unitName(unit.unitType)} ×${unit.count}`)
                  .join(", ")}{" "}
                · разом {total.toLocaleString("uk-UA")}
              </p>
            </div>

            {camping ? (
              <button
                type="button"
                onClick={() => recall.mutate(march.id)}
                disabled={recall.isPending}
                className="rounded-lg border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-white disabled:opacity-50"
              >
                Відкликати
              </button>
            ) : (
              <div className="flex items-center gap-2">
                <span className="font-mono text-slate-700">{formatRemaining(march.arrivesAt, now)}</span>
                {catalog.speedUpCost("March", march.arrivesAt, now, march.speedUpCostGems) > 0 && (
                  <button
                    type="button"
                    onClick={() => speedUp.mutate(march.id)}
                    disabled={speedUp.isPending}
                    className="rounded-lg border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-white disabled:opacity-50"
                  >
                    {speedUpLabel(catalog.speedUpCost("March", march.arrivesAt, now, march.speedUpCostGems))}
                  </button>
                )}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
