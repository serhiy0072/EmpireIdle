import type { MailLetterView } from "../../lib/queries/mail";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_TARGET } from "../../lib/queries/marches";
import { SCOUT_OUTCOMES } from "../../lib/queries/scouting";

interface Props {
  letter: MailLetterView;
  onOpen: () => void;
}

const date = (iso: string) =>
  new Date(iso).toLocaleString("uk-UA", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

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
            <span className="mr-1 inline-block h-2 w-2 rounded-full bg-emerald-500" aria-label="непрочитане" />
          )}
          Розвідка
          {report !== null && `: ${report.targetName === "" ? "ціль" : report.targetName} (${report.x}, ${report.y})`}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>
      {report === null ? (
        <p className="text-sm text-slate-500">Звіт уже не зберігся.</p>
      ) : report.outcome === "Success" ? (
        <>
          <p className="text-sm text-slate-700">
            Сила оборони:{" "}
            <span className="font-medium">{Math.round(report.defencePower ?? 0).toLocaleString("uk-UA")}</span>
            <span className="text-xs text-slate-500"> (з усіма бонусами)</span>
          </p>
          {report.targetType === MARCH_TARGET.village && (
            <p className="text-sm text-slate-600">
              Можна винести:{" "}
              {loot.length === 0
                ? "нічого"
                : loot.map(([key, amount]) => `${catalog.resourceName(key)} ${amount.toLocaleString("uk-UA")}`).join(", ")}
            </p>
          )}
        </>
      ) : (
        <p className="text-sm text-amber-800">{SCOUT_OUTCOMES[report.outcome] ?? report.outcome}</p>
      )}
    </li>
  );
}
