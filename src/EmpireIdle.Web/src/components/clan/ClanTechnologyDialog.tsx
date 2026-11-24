import type { ClanOfferingResult, ClanTechnologiesView, ClanTechnologyNodeView } from "../../lib/apiTypes";
import { formatNumber } from "../../lib/format";
import { useCatalog } from "../../lib/queries/catalog";
import { effectLabel, effectValue, technologyName, tierNumeral } from "../../lib/technologies";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";
import { GameButton, GameDialog } from "../game/GameUi";
import { t } from "i18next";

interface Props {
  node: ClanTechnologyNodeView;
  view: ClanTechnologiesView;
  now: number;
  /** Роль гравця може ставити мітку керівництва. */
  canMark: boolean;
  pending: boolean;
  /** Відмова останньої дії — у діалозі, бо банер сторінки під ним не видно. */
  error: unknown;
  /** Підсумок останнього підношення саме в цей вузол — щоб гравець бачив монети, що прийшли. */
  result: ClanOfferingResult | null;
  onOffer: () => void;
  onOfferGems: (price: number) => void;
  onMark: (marked: boolean) => void;
  onClose: () => void;
}

/**
 * Технологія клану: що дає рівень, поступ до наступного, ціна підношення її ресурсом і в gems.
 * Офіцер із правом ManageTechnologies ставить тут мітку, а внесок у позначений вузол платить більше.
 */
export default function ClanTechnologyDialog({ node, view, now, canMark, pending, error, result, onOffer, onOfferGems, onMark, onClose }: Props) {
  const catalog = useCatalog();

  const offering = view.offering;
  const maxed = node.level >= node.maxLevel;
  const available = node.availability === "Available";
  const marked = view.markedTechnology === `${node.tier}:${node.node}`;
  const bonus = Math.round(view.markedBonus * 100);
  const reason = blockedReason(node);
  const gemsLeft = offering.gemOfferingsToday < offering.gemDailyLimit;
  const share = node.nextPoints == null || node.nextPoints <= 0 ? 0 : Math.min(1, node.progress / node.nextPoints);

  return (
    <GameDialog
      size="sm"
      label={technologyName(node.node)}
      title={`${marked ? "⭐ " : ""}${technologyName(node.node)} ${tierNumeral(node.tier)}`}
      subtitle={t("technologies:detail.level", { level: node.level, max: node.maxLevel })}
      onClose={onClose}
    >
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

      {!maxed && node.nextPoints != null && (
        <div className="space-y-1 rounded-xl bg-black/20 p-3 text-sm">
          <p className="text-white/70">
            {t("technologies:clan.progress", { progress: formatNumber(node.progress), points: formatNumber(node.nextPoints) })}
          </p>
          <div className="h-2 overflow-hidden rounded-full bg-black/30">
            <div className="h-full rounded-full bg-gradient-to-r from-emerald-400 to-lime-300" style={{ width: `${share * 100}%` }} />
          </div>
          {node.resource != null && node.attemptCost != null && (
            <p>
              {t("technologies:clan.attemptCost", {
                cost: formatNumber(node.attemptCost),
                resource: catalog.resourceName(node.resource),
                points: offering.pointsPerAttempt,
              })}
            </p>
          )}
        </div>
      )}

      {marked && available && <p className="rounded-lg bg-yellow-200/90 px-3 py-1.5 text-sm text-yellow-950">{t("technologies:clan.markedHere", { bonus })}</p>}

      <ErrorBanner error={error} />

      {reason !== null && <p className="rounded-lg bg-amber-100/90 px-3 py-1.5 text-sm text-amber-900">{reason}</p>}

      {available && offering.attempts === 0 && (
        <p className="text-sm text-white/70">
          {offering.nextAttemptAt == null
            ? t("technologies:clan.noAttemptsNow")
            : t("technologies:clan.noAttempts", { remaining: formatRemaining(offering.nextAttemptAt, now) })}
        </p>
      )}
      {available && !gemsLeft && <p className="text-sm text-white/70">{t("technologies:clan.gemLimit")}</p>}

      {result !== null && (
        <p className="rounded-lg bg-emerald-100/90 px-3 py-1.5 text-sm text-emerald-900">
          {result.marked ? t("technologies:clan.resultMarked", { coins: result.coins }) : t("technologies:clan.result", { coins: result.coins })}
          {result.levelsCompleted.length > 0 && ` · ${t("technologies:clan.levelUp", { level: result.level })}`}
        </p>
      )}

      {available && (
        <div className="flex flex-wrap justify-end gap-2">
          {canMark && (
            <GameButton variant="secondary" disabled={pending} onClick={() => onMark(!marked)}>
              {marked ? t("technologies:clan.unmark") : t("technologies:clan.mark")}
            </GameButton>
          )}
          {offering.gemPrice != null && (
            <GameButton variant="secondary" disabled={pending || !gemsLeft} onClick={() => onOfferGems(offering.gemPrice!)}>
              {t("technologies:clan.offerGems", { price: offering.gemPrice, points: offering.gemPoints })}
            </GameButton>
          )}
          <GameButton variant="accent" disabled={pending || offering.attempts === 0} onClick={onOffer}>
            {t("technologies:clan.offer")}
          </GameButton>
        </div>
      )}
    </GameDialog>
  );
}

/** Чому підносити сюди не можна; null — можна. Ресурс і gems перевіряє сервер. */
function blockedReason(node: ClanTechnologyNodeView): string | null {
  switch (node.availability) {
    case "Available":
      return null;
    case "Maxed":
      return t("technologies:detail.maxed");
    case "PreviousTierIncomplete":
      return t("technologies:clan.previousTier");
    case "PrerequisitesMissing":
      return t("technologies:detail.prerequisites", { nodes: node.requires.map(technologyName).join(", ") });
    default:
      return node.availability;
  }
}
