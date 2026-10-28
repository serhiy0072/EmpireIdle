import type { MailLetterView } from "../../lib/queries/mail";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_TARGET } from "../../lib/queries/marches";
import { scoutOutcomeLabel } from "../../lib/queries/scouting";
import { formatMoment, formatNumber } from "../../lib/format";
import { t } from "i18next";

interface Props {
  letter: MailLetterView;
  onOpen: () => void;
}

const date = (iso: string) => formatMoment(iso);

/**
 * Лист зі звітом розвідки: сила оборони з усіма бонусами й що можна винести.
 * Знімок моменту — на дату варто дивитись, перш ніж іти в напад.
 */
export default function ScoutReportLetter({ letter, onOpen }: Props) {
  const catalog = useCatalog();
  const report = letter.scoutReport ?? null;
  const loot = report === null ? [] : Object.entries(report.lootable).filter(([, amount]) => amount > 0);

  return (
    <li
      className="space-y-1 rounded-xl border border-amber-200 bg-white p-3"
      onMouseEnter={() => !letter.isRead && onOpen()}
      onClick={() => !letter.isRead && onOpen()}
    >
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-slate-800">
          {!letter.isRead && (
            <span className="mr-1 inline-block h-2 w-2 rounded-full bg-emerald-500" aria-label={t("social:mail.unread")} />
          )}
          {t("social:scout.title")}
          {report !== null && `: ${report.targetName === "" ? t("social:scout.target") : report.targetName} (${report.x}, ${report.y})`}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>
      {report === null ? (
        <p className="text-sm text-slate-500">{t("social:scout.gone")}</p>
      ) : report.outcome === "Success" ? (
        <>
          <p className="text-sm text-slate-700">
            {t("social:scout.defence")}{" "}
            <span className="font-medium">{formatNumber(Math.round(report.defencePower ?? 0))}</span>
            <span className="text-xs text-slate-500"> {t("social:scout.withBonuses")}</span>
          </p>
          {report.targetType === MARCH_TARGET.village && (
            <p className="text-sm text-slate-600">
              {t("social:scout.loot")}{" "}
              {loot.length === 0
                ? t("social:scout.nothing")
                : loot.map(([key, amount]) => `${catalog.resourceName(key)} ${formatNumber(amount)}`).join(", ")}
            </p>
          )}
        </>
      ) : (
        <p className="text-sm text-amber-800">{scoutOutcomeLabel(report.outcome)}</p>
      )}
    </li>
  );
}
