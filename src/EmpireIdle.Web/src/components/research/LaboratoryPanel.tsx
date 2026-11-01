import { useNow } from "../../hooks/useNow";
import { useResearch } from "../../lib/queries/research";
import { technologyName, tierNumeral } from "../../lib/technologies";
import { formatRemaining } from "../../lib/time";
import { t } from "i18next";

interface Props {
  playerId: string;
}

/**
 * Лабораторія на картці будівлі: що досліджується й скільки лишилось, щоб не відкривати
 * дерево заради одного погляду. Керування (старт, прискорення, допомога) — на екрані лабораторії.
 */
export default function LaboratoryPanel({ playerId }: Props) {
  const now = useNow();
  const research = useResearch(playerId);
  const active = research.data?.active ?? null;

  if (research.data === undefined || !research.data.unlocked) return null;

  if (active === null) {
    return <p className="border-t border-slate-200 pt-3 text-sm text-slate-500">{t("technologies:page.idle")}</p>;
  }

  const from = new Date(active.startedAt).getTime();
  const to = new Date(active.completesAt).getTime();
  const share = to <= from ? 1 : Math.min(1, Math.max(0, (now - from) / (to - from)));

  return (
    <div className="space-y-1.5 border-t border-slate-200 pt-3 text-sm">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <span className="font-medium text-slate-700">
          {t("technologies:page.researching", {
            name: `${technologyName(active.node)} ${tierNumeral(active.tier)}`,
            level: active.level,
          })}
        </span>
        <span className="text-slate-500">{t("technologies:page.remaining", { remaining: formatRemaining(active.completesAt, now) })}</span>
      </div>
      <div className="h-1.5 overflow-hidden rounded-full bg-slate-200">
        <div className="h-full rounded-full bg-gradient-to-r from-emerald-400 to-lime-400" style={{ width: `${share * 100}%` }} />
      </div>
    </div>
  );
}
