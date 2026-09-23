import type { BuildingResponse, ClanHelpItemResponse, ClanHelpTarget, TrainingOrderResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { formatRemaining } from "../../lib/time";

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

const button = "rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50";

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
        <h3 className="text-sm font-medium uppercase tracking-wide text-slate-500">Просять допомоги</h3>
        {others.length === 0 ? (
          <p className="text-sm text-slate-500">Зараз нікому не потрібна допомога.</p>
        ) : (
          others.map((item) => (
            <div key={item.requestId} className="flex flex-wrap items-center gap-2 rounded-xl border border-slate-200 bg-white p-3">
              <div className="flex-1 text-sm">
                <span className="font-medium text-slate-800">{item.playerName}</span>
                <span className="ml-2 text-slate-500">{item.targetType === "Construction" ? "будівництво" : "тренування"}</span>
                <div className="text-xs text-slate-500">
                  {item.helpCount}/{item.maxHelpers} · ще {formatRemaining(item.expiresAt, now)}
                </div>
              </div>
              <button
                type="button"
                onClick={() => onGive(item.requestId)}
                disabled={busy || item.alreadyHelped || item.helpCount >= item.maxHelpers}
                className={
                  item.alreadyHelped
                    ? "rounded-lg bg-slate-100 px-3 py-1 text-sm text-slate-500"
                    : "rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                }
              >
                {item.alreadyHelped ? "Допомогли" : "Допомогти"}
              </button>
            </div>
          ))
        )}
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-medium uppercase tracking-wide text-slate-500">Мої прохання</h3>
        {mine.map((item) => (
          <div key={item.requestId} className="rounded-xl border border-emerald-200 bg-white p-3 text-sm">
            <span className="text-slate-800">{item.targetType === "Construction" ? "Будівництво" : "Тренування"}</span>
            <span className="ml-2 text-xs text-slate-500">
              допомогли {item.helpCount}/{item.maxHelpers} · ще {formatRemaining(item.expiresAt, now)}
            </span>
          </div>
        ))}

        {constructions
          .filter((building) => !requested.has(building.id))
          .map((building) => (
            <div key={building.id} className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white p-3 text-sm">
              <span className="flex-1 text-slate-700">
                {catalog.buildingName(building.type)} → {building.level + 1}
              </span>
              <button type="button" onClick={() => onRequest(0, building.id)} disabled={busy} className={button}>
                Попросити
              </button>
            </div>
          ))}

        {trainings
          .filter((order) => !requested.has(order.id))
          .map((order) => (
            <div key={order.id} className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white p-3 text-sm">
              <span className="flex-1 text-slate-700">
                {catalog.unitName(order.unitType)} ×{order.count}
              </span>
              <button type="button" onClick={() => onRequest(1, order.id)} disabled={busy} className={button}>
                Попросити
              </button>
            </div>
          ))}

        {mine.length === 0 && constructions.length === 0 && trainings.length === 0 && (
          <p className="text-sm text-slate-500">Немає черг, які можна прискорити.</p>
        )}
      </section>
    </div>
  );
}
