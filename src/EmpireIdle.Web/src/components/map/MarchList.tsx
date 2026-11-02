import { useNow } from "../../hooks/useNow";
import type { MarchResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_INTENT, MARCH_STATE, MARCH_TARGET, useRecallCamp, useSpeedUpMarch } from "../../lib/queries/marches";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";
import SpeedUpItemsButton from "../SpeedUpItemsButton";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";

interface Props {
  playerId: string;
  marches: MarchResponse[];
}

/** Активні походи: куди, скільки, коли прибуде, і прискорення на 50/75/90% за gems; табори — з відкликанням. */
export default function MarchList({ playerId, marches }: Props) {
  const now = useNow();
  const catalog = useCatalog();
  const speedUp = useSpeedUpMarch(playerId);
  const recall = useRecallCamp(playerId);

  if (marches.length === 0) {
    return <p className="text-sm text-slate-500">{t("map:marches.none")}</p>;
  }

  return (
    <div className="space-y-2">
      <ErrorBanner error={speedUp.error ?? recall.error} />

      {marches.map((march) => {
        const returning = march.state === MARCH_STATE.returning;
        const camping = march.state === MARCH_STATE.camping;
        const total = march.units.reduce((sum, unit) => sum + unit.count, 0);
        const level = march.targetLevel != null ? ` ${t("map:level", { level: march.targetLevel })}` : "";
        const place = march.targetName != null ? `${march.targetName}${level}` : `(${march.targetX}, ${march.targetY})`;
        const target = march.targetType === MARCH_TARGET.clanStructure ? t("map:marches.structure", { place }) : place;

        return (
          <div key={march.id} className="flex items-center justify-between gap-3 rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <div>
              <p className="text-slate-800">
                {camping ? (
                  <>{t("map:marches.camp", { x: march.targetX, y: march.targetY, target })}</>
                ) : (
                  <>
                    {returning
                      ? t("map:marches.returning", { target })
                      : march.intent === MARCH_INTENT.scout
                        ? t("map:marches.scouting", { target })
                        : t("map:marches.going", { target })}
                  </>
                )}
              </p>
              <p className="text-xs text-slate-500">
                {march.units
                  .map((unit) => `${catalog.unitName(unit.unitType)} ×${unit.count}`)
                  .join(", ")}{" "}
                {t("map:marches.total", { count: formatNumber(total) })}
              </p>
            </div>

            {camping ? (
              <button
                type="button"
                onClick={() => recall.mutate(march.id)}
                disabled={recall.isPending}
                className="rounded-lg border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-white disabled:opacity-50"
              >
                {t("map:marches.recall")}
              </button>
            ) : (
              <div className="flex flex-col items-end gap-1">
                <span className="flex items-center gap-2">
                  <span className="font-mono text-slate-700">{formatRemaining(march.arrivesAt, now)}</span>
                  <SpeedUpItemsButton timer="March" targetId={march.id} completesAt={march.arrivesAt} />
                </span>
                {/* На межі (останні секунди) зрізати нічого — кнопок не показуємо, сервер усе одно відмовить */}
                {new Date(march.arrivesAt).getTime() - now > catalog.speedUpFloorSeconds("March") * 1_000 && (
                  <span className="flex gap-1">
                    {catalog.marchSpeedUps.map((option) => (
                      <button
                        key={option.percent}
                        type="button"
                        onClick={() => speedUp.mutate({ marchId: march.id, percent: option.percent })}
                        disabled={speedUp.isPending}
                        title={t("map:marches.speedUp", { percent: option.percent, gems: option.gems })}
                        className="rounded-lg bg-gradient-to-b from-sky-400 to-blue-600 px-1.5 py-0.5 text-[11px] font-semibold text-white shadow hover:brightness-110 disabled:opacity-50"
                      >
                        −{option.percent}% · {option.gems}💎
                      </button>
                    ))}
                  </span>
                )}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
