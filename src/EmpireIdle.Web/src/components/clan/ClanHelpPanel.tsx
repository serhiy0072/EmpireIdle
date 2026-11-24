import type { BuildingResponse, ClanHelpItemResponse, ClanHelpTarget, TrainingOrderResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { technologyKeyLabel } from "../../lib/technologies";
import { formatRemaining } from "../../lib/time";
import { t } from "i18next";

interface Props {
  items: ClanHelpItemResponse[];
  /** Мої будівлі в процесі апгрейду й замовлення тренування — кандидати на прохання. */
  constructions: BuildingResponse[];
  trainings: TrainingOrderResponse[];
  now: number;
  busy: boolean;
  onGive: (requestId: string) => void;
  onRequest: (targetType: ClanHelpTarget, targetId: string) => void;
}

/** Ціль прохання з контракту (рядок) — ключ підпису. */
const HELP_LABEL: Record<string, "construction" | "training" | "research"> = {
  Construction: "construction",
  Training: "training",
  Research: "research",
};
const HELP_TITLE: Record<string, "Construction" | "Training" | "Research"> = {
  Construction: "Construction",
  Training: "Training",
  Research: "Research",
};

/** Що саме прискорюють: назва технології, якщо сервер її назвав, інакше загальний підпис цілі. */
function subjectOf(item: ClanHelpItemResponse, fallback: string): string {
  return (item.subject == null ? null : technologyKeyLabel(item.subject)) ?? fallback;
}

const button = "rounded-lg border border-[#4a72b0] px-3 py-1 text-sm text-slate-100 hover:bg-white/10 disabled:opacity-50";

/**
 * Взаємодопомога: кожен клік соклановця скорочує таймер. Своє прохання
 * створюється з поточних черг — без вибору серед того, чого нема.
 */
export default function ClanHelpPanel({ items, constructions, trainings, now, busy, onGive, onRequest }: Props) {
  const catalog = useCatalog();
  const requested = new Set(items.filter((item) => item.isMine).map((item) => item.targetId));
  const mine = items.filter((item) => item.isMine);
  const others = items.filter((item) => !item.isMine);

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <section className="space-y-2">
        <h3 className="text-sm font-medium uppercase tracking-wide text-sky-100/70">{t("social:help.asking")}</h3>
        {others.length === 0 ? (
          <p className="text-sm text-sky-100/70">{t("social:help.none")}</p>
        ) : (
          others.map((item) => (
            <div key={item.requestId} className="flex flex-wrap items-center gap-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-3">
              <div className="flex-1 text-sm">
                <span className="font-medium text-white">{item.playerName}</span>
                <span className="ml-2 text-sky-100/70">{subjectOf(item, t(`social:help.${HELP_LABEL[item.targetType] ?? "training"}`))}</span>
                <div className="text-xs text-sky-100/70">
                  {t("social:help.progress", { count: item.helpCount, max: item.maxHelpers, remaining: formatRemaining(item.expiresAt, now) })}
                </div>
              </div>
              <button
                type="button"
                onClick={() => onGive(item.requestId)}
                disabled={busy || item.alreadyHelped || item.helpCount >= item.maxHelpers}
                className={
                  item.alreadyHelped
                    ? "rounded-lg bg-white/10 px-3 py-1 text-sm text-sky-100/70"
                    : "rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                }
              >
                {item.alreadyHelped ? t("social:help.helped") : t("social:help.help")}
              </button>
            </div>
          ))
        )}
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-medium uppercase tracking-wide text-sky-100/70">{t("social:help.mine")}</h3>
        {mine.map((item) => (
          <div key={item.requestId} className="rounded-xl border border-emerald-400/50 bg-[#1d3b6f] p-3 text-sm">
            <span className="text-white">{subjectOf(item, t(`social:help.${HELP_TITLE[item.targetType] ?? "Training"}`))}</span>
            <span className="ml-2 text-xs text-sky-100/70">
              {t("social:help.mineProgress", { count: item.helpCount, max: item.maxHelpers, remaining: formatRemaining(item.expiresAt, now) })}
            </span>
          </div>
        ))}

        {constructions
          .filter((building) => !requested.has(building.id))
          .map((building) => (
            <div key={building.id} className="flex items-center gap-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-3 text-sm">
              <span className="flex-1 text-slate-100">
                {catalog.buildingName(building.type)} → {building.level + 1}
              </span>
              <button type="button" onClick={() => onRequest("Construction", building.id)} disabled={busy} className={button}>
                {t("social:help.ask")}
              </button>
            </div>
          ))}

        {trainings
          .filter((order) => !requested.has(order.id))
          .map((order) => (
            <div key={order.id} className="flex items-center gap-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-3 text-sm">
              <span className="flex-1 text-slate-100">
                {catalog.unitName(order.unitType)} ×{order.count}
              </span>
              <button type="button" onClick={() => onRequest("Training", order.id)} disabled={busy} className={button}>
                {t("social:help.ask")}
              </button>
            </div>
          ))}

        {mine.length === 0 && constructions.length === 0 && trainings.length === 0 && (
          <p className="text-sm text-sky-100/70">{t("social:help.nothingToAsk")}</p>
        )}
      </section>
    </div>
  );
}
