import { useHotkeys } from "../../hooks/useHotkeys";
import type { ResearchNodeView } from "../../lib/apiTypes";
import { formatNumber } from "../../lib/format";
import { useCatalog } from "../../lib/queries/catalog";
import { effectLabel, effectValue, technologyName, tierNumeral } from "../../lib/technologies";
import { formatDuration } from "../../lib/time";
import { GameButton } from "../game/GameUi";
import { t } from "i18next";

interface Props {
  node: ResearchNodeView;
  /** Лабораторія вже щось досліджує — старт недоступний, хоч сам вузол відкритий. */
  busy: boolean;
  pending: boolean;
  onStart: () => void;
  onClose: () => void;
}

/** Вузол технології: що дає рівень, скільки вже дав, ціна й час наступного; причина, коли старт закритий. */
export default function ResearchNodeDialog({ node, busy, pending, onStart, onClose }: Props) {
  const catalog = useCatalog();

  useHotkeys({ Escape: onClose }, { modal: true });

  const maxed = node.level >= node.maxLevel;
  const reason = blockedReason(node, busy);

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={technologyName(node.node)}
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4"
    >
      <div
        onClick={(event) => event.stopPropagation()}
        className="w-full max-w-sm space-y-3 rounded-2xl bg-gradient-to-b from-[#24467f] to-[#16305e] p-4 text-white shadow-2xl ring-2 ring-sky-300"
      >
        <div className="flex items-start gap-2">
          <div className="flex-1">
            <h2 className="text-lg font-semibold">
              {technologyName(node.node)} {tierNumeral(node.tier)}
            </h2>
            <p className="text-sm text-white/70">{t("technologies:detail.level", { level: node.level, max: node.maxLevel })}</p>
          </div>
          <button type="button" aria-label={t("technologies:detail.close")} onClick={onClose} className="text-xl font-bold text-white/60 hover:text-white">
            ✕
          </button>
        </div>

        <table className="w-full text-sm">
          <thead className="text-xs text-white/60">
            <tr>
              <th />
              <th className="text-right font-normal">{t("technologies:detail.perLevel")}</th>
              <th className="text-right font-normal">{t("technologies:detail.total")}</th>
            </tr>
          </thead>
          <tbody>
            {node.effects.map((effect) => (
              <tr key={`${effect.stat}:${effect.target ?? ""}`}>
                <td className="py-0.5">{effectLabel(effect, catalog)}</td>
                <td className="text-right">{effectValue(effect.stat, effect.perLevel)}</td>
                <td className="text-right font-semibold">{effectValue(effect.stat, effect.perLevel * node.level)}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {!maxed && node.nextCost != null && (
          <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 rounded-xl bg-black/20 p-3 text-sm">
            <dt className="text-white/60">{t("technologies:detail.cost")}</dt>
            <dd>{node.nextCost.map((cost) => `${formatNumber(cost.amount)} ${catalog.resourceName(cost.resource)}`).join(", ")}</dd>
            {node.nextSeconds != null && (
              <>
                <dt className="text-white/60">{t("technologies:detail.time")}</dt>
                <dd>{formatDuration(node.nextSeconds * 1_000)}</dd>
              </>
            )}
          </dl>
        )}

        {reason !== null && <p className="rounded-lg bg-amber-100/90 px-3 py-1.5 text-sm text-amber-900">{reason}</p>}

        {!maxed && (
          <div className="flex justify-end">
            <GameButton variant="accent" disabled={reason !== null || pending} onClick={onStart}>
              {t("technologies:detail.start")}
            </GameButton>
          </div>
        )}
      </div>
    </div>
  );
}

/** Чому старт закритий; null — можна. Ціну в ресурсах перевіряє сервер. */
function blockedReason(node: ResearchNodeView, busy: boolean): string | null {
  switch (node.availability) {
    case "Available":
      return busy ? t("technologies:detail.busy") : null;
    case "Maxed":
      return t("technologies:detail.maxed");
    case "TierGated":
      return t("technologies:detail.tierGated");
    case "PreviousTierIncomplete":
      return t("technologies:detail.previousTier");
    case "PrerequisitesMissing":
      return t("technologies:detail.prerequisites", { nodes: node.requires.map(technologyName).join(", ") });
    default:
      return node.availability;
  }
}
