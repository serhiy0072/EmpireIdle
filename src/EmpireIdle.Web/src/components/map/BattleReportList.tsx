import { useState } from "react";
import type { BattleReportResponse } from "../../lib/apiTypes";
import { useBattleReports, useMarkReportRead } from "../../lib/queries/battleReports";
import { useCatalog } from "../../lib/queries/catalog";
import ErrorBanner from "../ErrorBanner";
import { formatDateTime, formatNumber } from "../../lib/format";
import { t } from "i18next";

interface Props {
  playerId: string;
}

function when(value: string): string {
  return formatDateTime(value, { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
}

/** Звіти боїв: непрочитані жирним, розгортання позначає прочитаним. */
export default function BattleReportList({ playerId }: Props) {
  const catalog = useCatalog();
  const reports = useBattleReports(playerId);
  const markRead = useMarkReportRead(playerId);
  const [openId, setOpenId] = useState<string | null>(null);

  if (reports.isPending) {
    return <p className="text-sm text-slate-500">{t("map:reports.loading")}</p>;
  }

  if (reports.isError) {
    return <ErrorBanner error={reports.error} />;
  }

  if (reports.data.length === 0) {
    return <p className="text-sm text-slate-500">{t("map:reports.none")}</p>;
  }

  const open = (report: BattleReportResponse) => {
    setOpenId((current) => (current === report.id ? null : report.id));

    if (!report.isRead) markRead.mutate(report.id);
  };

  return (
    <div className="space-y-2">
      {reports.data.map((report) => (
        <div key={report.id} className="rounded-lg border border-slate-200 bg-white">
          <button
            type="button"
            onClick={() => open(report)}
            className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm"
          >
            <span className={report.isRead ? "text-slate-700" : "font-semibold text-slate-900"}>
              {report.won ? t("map:reports.victory") : t("map:reports.defeat")} · {report.targetName}
              {report.targetLevel > 0 && ` ${t("map:level", { level: report.targetLevel })}`}
              {report.tamedBeastKey != null && ` · ${t("map:reports.tamed", { beast: catalog.beastName(report.tamedBeastKey) })}`}
            </span>
            <span className="flex items-center gap-2 text-xs text-slate-500">
              <span
                aria-hidden
                className={`rounded px-2 py-0.5 ${report.won ? "bg-emerald-100 text-emerald-800" : "bg-red-100 text-red-800"}`}
              >
                {report.won ? "✓" : "✕"}
              </span>
              {when(report.foughtAt)}
            </span>
          </button>

          {openId === report.id && (
            <div className="space-y-2 border-t border-slate-100 px-3 py-2 text-sm">
              <p className="text-xs text-slate-500">
                {t("map:reports.powers", {
                  x: report.x,
                  y: report.y,
                  attacker: Math.round(report.attackerPower),
                  defender: Math.round(report.defenderPower),
                })}
              </p>
              {report.heroExperience > 0 && (
                <p className="text-xs font-medium text-amber-700">
                  {t("map:reports.heroExperience", { amount: formatNumber(report.heroExperience) })}
                </p>
              )}
              <table className="w-full text-xs">
                <thead className="text-slate-500">
                  <tr>
                    <th className="text-left font-medium">{t("map:reports.unit")}</th>
                    <th className="text-right font-medium">{t("map:reports.sent")}</th>
                    <th className="text-right font-medium">{t("map:reports.survived")}</th>
                    <th className="text-right font-medium">{t("map:reports.wounded")}</th>
                    <th className="text-right font-medium">{t("map:reports.recoverable")}</th>
                    <th className="text-right font-medium">{t("map:reports.dead")}</th>
                  </tr>
                </thead>
                <tbody>
                  {report.lines.map((line) => (
                    <tr key={line.unitType} className="text-slate-700">
                      <td>{catalog.unitName(line.unitType)}</td>
                      <td className="text-right">{line.sent}</td>
                      <td className="text-right">{line.survived}</td>
                      <td className="text-right">{line.wounded}</td>
                      <td className="text-right">{line.recoverable}</td>
                      <td className="text-right">{line.dead}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
