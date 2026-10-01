import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_TARGET } from "../../lib/queries/marches";
import { SCOUT_OUTCOMES, useScoutReports } from "../../lib/queries/scouting";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
}

function when(value: string): string {
  return new Date(value).toLocaleString("uk-UA", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
}

/**
 * Звіти розвідки: сила оборони з усіма бонусами й що можна винести.
 * Знімок моменту — на дату варто дивитись, перш ніж іти в напад.
 */
export default function ScoutReportList({ playerId }: Props) {
  const catalog = useCatalog();
  const reports = useScoutReports(playerId);

  if (reports.isPending) return <p className="text-sm text-slate-500">Завантаження звітів…</p>;
  if (reports.isError) return <ErrorBanner error={reports.error} />;
  if (reports.data.length === 0) return <p className="text-sm text-slate-500">Розвідок ще не було.</p>;

  return (
    <ul className="space-y-2">
      {reports.data.map((report) => {
        const success = report.outcome === "Success";
        const loot = Object.entries(report.lootable).filter(([, amount]) => amount > 0);

        return (
          <li key={report.id} className="space-y-1 rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <div className="flex items-baseline justify-between gap-2">
              <span className="font-medium text-slate-800">
                {report.targetName === "" ? "Ціль" : report.targetName} ({report.x}, {report.y})
              </span>
              <span className="text-xs text-slate-400">{when(report.createdAt)}</span>
            </div>
            {success ? (
              <>
                <p className="text-slate-700">
                  Сила оборони: <span className="font-medium">{Math.round(report.defencePower ?? 0).toLocaleString("uk-UA")}</span>
                  <span className="text-xs text-slate-500"> (з усіма бонусами)</span>
                </p>
                {report.targetType === MARCH_TARGET.village && (
                  <p className="text-slate-600">
                    Можна винести:{" "}
                    {loot.length === 0
                      ? "нічого"
                      : loot.map(([key, amount]) => `${catalog.resourceName(key)} ${amount.toLocaleString("uk-UA")}`).join(", ")}
                  </p>
                )}
              </>
            ) : (
              <p className="text-amber-800">{SCOUT_OUTCOMES[report.outcome] ?? report.outcome}</p>
            )}
          </li>
        );
      })}
    </ul>
  );
}
