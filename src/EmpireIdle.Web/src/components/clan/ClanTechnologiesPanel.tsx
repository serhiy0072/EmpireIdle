import { useState } from "react";
import type { ClanOfferingResult, ClanTechnologiesView } from "../../lib/apiTypes";
import { formatNumber } from "../../lib/format";
import { useCatalog } from "../../lib/queries/catalog";
import { useMarkClanTechnology, useOfferClanGems, useOfferClanResources } from "../../lib/queries/clanTechnologies";
import { effectLabel, effectValue, technologyKeyLabel, technologyTabName } from "../../lib/technologies";
import { formatRemaining } from "../../lib/time";
import { GameTabs } from "../game/GameUi";
import { TierTree } from "../research/TechnologyTree";
import { RewardLine } from "../tower/TowerPanels";
import ClanTechnologyDialog from "./ClanTechnologyDialog";
import { latestError } from "../../lib/latestError";
import { t } from "i18next";

interface Props {
  playerId: string;
  view: ClanTechnologiesView;
  now: number;
  /** Роль має ManageTechnologies — може ставити мітку керівництва. */
  canMark: boolean;
}

/**
 * Кланові технології (переробка 12.11.2026): гравець сам обирає технологію й підносить у неї її ресурс.
 * Шар вкладки відкривається, коли кожна технологія попереднього шару має +1; тір — так само від останнього шару.
 */
export default function ClanTechnologiesPanel({ playerId, view, now, canMark }: Props) {
  const catalog = useCatalog();
  const offer = useOfferClanResources(playerId);
  const offerGems = useOfferClanGems(playerId);
  const mark = useMarkClanTechnology(playerId);

  const [tab, setTab] = useState<string | null>(null);
  const [picked, setPicked] = useState<{ tier: number; node: string } | null>(null);
  const [result, setResult] = useState<{ key: string; value: ClanOfferingResult } | null>(null);

  const activeTab = tab ?? view.tabs[0] ?? "";
  const offering = view.offering;
  const selected = picked === null ? null : (view.nodes.find((node) => node.tier === picked.tier && node.node === picked.node) ?? null);
  const pickedKey = picked === null ? null : `${picked.tier}:${picked.node}`;
  const finished = view.nodes.length > 0 && view.nodes.every((node) => node.level >= node.maxLevel);
  const markedLabel = view.markedTechnology == null ? null : (technologyKeyLabel(view.markedTechnology) ?? view.markedTechnology);
  const bonus = Math.round(view.markedBonus * 100);
  const daily = view.daily;
  const contributors = [...view.contributors].sort((a, b) => b.today - a.today || b.total - a.total);
  const failure = latestError([offer, offerGems, mark]);

  const remember = (key: string) => (value: ClanOfferingResult) => setResult({ key, value });

  return (
    <div className="space-y-4">
      <section className="grid gap-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4 text-sm text-slate-100 sm:grid-cols-2">
        <p>
          {t("technologies:clan.coins", { coins: formatNumber(view.coins) })}
        </p>
        <p>
          {t("technologies:clan.attempts", { attempts: offering.attempts, max: offering.maxAttempts })}
          {offering.nextAttemptAt != null && (
            <span className="text-sky-100/70"> · {t("technologies:clan.nextAttempt", { remaining: formatRemaining(offering.nextAttemptAt, now) })}</span>
          )}
        </p>
        <p>{t("technologies:clan.gems", { count: offering.gemOfferingsToday, limit: offering.gemDailyLimit })}</p>
        <p className={markedLabel === null ? "text-sky-100/70" : "font-medium text-amber-200"}>
          {markedLabel === null ? t("technologies:clan.noMark") : t("technologies:clan.marked", { name: markedLabel, bonus })}
        </p>
      </section>

      <section className="space-y-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4 text-sm">
        <h3 className="font-medium text-white">{t("technologies:clan.daily", { points: formatNumber(daily.pointsToday) })}</h3>
        <ul className="space-y-1">
          {daily.rewards.map((reward) => (
            <li key={reward.points} className={`flex flex-wrap justify-between gap-2 ${reward.reached ? "text-emerald-200" : "text-slate-200"}`}>
              <span>
                {reward.reached ? "✓ " : ""}
                {t("technologies:clan.threshold", { points: formatNumber(reward.points) })}
              </span>
              <RewardLine rewards={reward.rewards} />
            </li>
          ))}
        </ul>
      </section>

      {/* Вкладки дерева — ті самі «картки», що й у Лабораторії: дерево одне на вигляд в обох місцях */}
      <div className="rounded-t-xl bg-[#16305e] px-2 pt-2">
        <GameTabs tabs={view.tabs.map((key) => ({ key, label: technologyTabName(key) }))} value={activeTab} onChange={setTab} />
      </div>

      <div className="!mt-0 space-y-6 rounded-b-xl bg-[#1d3b6f] px-2 py-4 text-white">
        {finished && <p className="rounded-lg bg-amber-500/35 px-3 py-2 text-sm text-amber-950">{t("technologies:clan.finished")}</p>}
        {view.tiers.map((tier) => (
          <TierTree
            key={tier.tier}
            nodes={view.nodes}
            tier={tier.tier}
            tab={activeTab}
            open={tier.openTabs.includes(activeTab)}
            lockedHint={t("technologies:clan.tierLocked")}
            markedKey={view.markedTechnology}
            onPick={(node) => {
              setPicked({ tier: node.tier, node: node.node });
              offer.reset();
              offerGems.reset();
              mark.reset();
            }}
          />
        ))}
      </div>

      <section className="space-y-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4 text-sm">
        <h3 className="font-medium text-white">{t("technologies:clan.bonuses")}</h3>
        {view.bonuses.length === 0 ? (
          <p className="text-sky-100/70">{t("technologies:page.noBonuses")}</p>
        ) : (
          <ul className="grid gap-x-4 gap-y-0.5 sm:grid-cols-2">
            {view.bonuses.map((item) => (
              <li key={`${item.stat}:${item.target ?? ""}`} className="flex justify-between gap-2">
                <span className="text-slate-200">{effectLabel(item, catalog)}</span>
                <span className="font-semibold text-white">{effectValue(item.stat, item.value)}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="space-y-2 rounded-xl border border-[#335c9a] bg-[#1d3b6f] p-4 text-sm">
        <h3 className="font-medium text-white">{t("technologies:clan.contributors")}</h3>
        {contributors.length === 0 ? (
          <p className="text-sky-100/70">{t("technologies:clan.noContributors")}</p>
        ) : (
          <table className="w-full">
            <thead className="text-xs text-sky-100/70">
              <tr>
                <th className="text-left font-normal" />
                <th className="text-right font-normal">{t("technologies:clan.today")}</th>
                <th className="text-right font-normal">{t("technologies:clan.total")}</th>
              </tr>
            </thead>
            <tbody>
              {contributors.map((contributor) => (
                <tr key={contributor.playerId} className={contributor.playerId === playerId ? "font-medium text-white" : "text-slate-100"}>
                  <td className="py-0.5">{contributor.name}</td>
                  <td className="text-right">{formatNumber(contributor.today)}</td>
                  <td className="text-right">{formatNumber(contributor.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {selected !== null && pickedKey !== null && (
        <ClanTechnologyDialog
          node={selected}
          view={view}
          now={now}
          canMark={canMark}
          pending={offer.isPending || offerGems.isPending || mark.isPending}
          error={failure}
          result={result?.key === pickedKey ? result.value : null}
          onOffer={() => offer.mutate({ tier: selected.tier, node: selected.node }, { onSuccess: remember(pickedKey) })}
          onOfferGems={(price) =>
            offerGems.mutate({ tier: selected.tier, node: selected.node, expectedPrice: price }, { onSuccess: remember(pickedKey) })
          }
          onMark={(marked) => mark.mutate(marked ? { tier: selected.tier, node: selected.node } : null)}
          onClose={() => setPicked(null)}
        />
      )}
    </div>
  );
}
