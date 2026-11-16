import { useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GameScreen, GameTabs } from "../components/game/GameUi";
import ResearchNodeDialog from "../components/research/ResearchNodeDialog";
import { TierTree } from "../components/research/TechnologyTree";
import SpeedUpItemsButton from "../components/SpeedUpItemsButton";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import { useClanHelp, useMyClan, useRequestHelp } from "../lib/queries/clans";
import { useResearch, useSpeedUpResearch, useStartResearch } from "../lib/queries/research";
import { speedUpLabel } from "../lib/speedUp";
import { effectLabel, effectValue, technologyName, tierNumeral } from "../lib/technologies";
import { formatRemaining } from "../lib/time";
import { t } from "i18next";

/**
 * Лабораторія (рішення 28.10.2026): особисті технології вкладками Битва / Економіка / Розвиток.
 * Тіри йдуть згори вниз, як на референсі; одне дослідження за раз, прискорення gems і предметами,
 * клан скорочує час допомогами.
 */
export default function LaboratoryPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const now = useNow();

  const research = useResearch(playerId);
  const start = useStartResearch(playerId);
  const speedUp = useSpeedUpResearch(playerId);
  const clan = useMyClan(playerId);
  const inClan = clan.data != null;
  const help = useClanHelp(playerId, inClan);
  const requestHelp = useRequestHelp(playerId);

  const [tab, setTab] = useState<string | null>(null);
  const [picked, setPicked] = useState<{ tier: number; node: string } | null>(null);

  if (research.isPending) return <p className="text-sm text-slate-500">{t("technologies:page.loading")}</p>;
  if (research.isError) return <ErrorBanner error={research.error} />;

  const view = research.data;

  if (!view.unlocked) {
    return (
      <GameScreen title={t("technologies:page.title")}>
        <p className="rounded-lg bg-white/10 px-3 py-2 text-sm">{t("technologies:page.locked", { level: view.requiredMainBuildingLevel })}</p>
      </GameScreen>
    );
  }

  const activeTab = tab ?? view.tabs[0] ?? "";
  const active = view.active ?? null;
  const helpAsked =
    view.researchId != null && (help.data ?? []).some((item) => item.isMine && item.targetId === view.researchId);
  const gems = active === null ? 0 : catalog.speedUpCost("Research", active.completesAt, now, 0);
  const failure = start.error ?? speedUp.error ?? requestHelp.error;
  const selected = picked === null ? null : (view.nodes.find((node) => node.tier === picked.tier && node.node === picked.node) ?? null);

  return (
    <GameScreen title={t("technologies:page.title")}>
      <div className="space-y-3">
        <ErrorBanner error={failure} />

        {/* Поточне дослідження — над вкладками, бо воно одне на всі */}
        <div className="rounded-xl bg-white/10 p-3 text-sm">
          {active === null ? (
            <p className="text-white/80">{t("technologies:page.idle")}</p>
          ) : (
            <div className="space-y-2">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <span className="font-semibold">
                  {t("technologies:page.researching", {
                    name: `${technologyName(active.node)} ${tierNumeral(active.tier)}`,
                    level: active.level,
                  })}
                </span>
                <span className="text-white/80">{t("technologies:page.remaining", { remaining: formatRemaining(active.completesAt, now) })}</span>
              </div>
              <Progress startedAt={active.startedAt} completesAt={active.completesAt} now={now} />
              <div className="flex flex-wrap items-center justify-end gap-2">
                {inClan && view.researchId != null && (
                  <GameButton
                    variant="secondary"
                    disabled={helpAsked || requestHelp.isPending}
                    onClick={() => requestHelp.mutate({ targetType: "Research", targetId: view.researchId! })}
                  >
                    {helpAsked ? t("technologies:page.helpAsked") : t("technologies:page.askHelp")}
                  </GameButton>
                )}
                {view.researchId != null && (
                  <SpeedUpItemsButton timer="Research" targetId={view.researchId} completesAt={active.completesAt} />
                )}
                {gems > 0 && (
                  <GameButton variant="accent" disabled={speedUp.isPending} onClick={() => speedUp.mutate()}>
                    {speedUpLabel(gems)}
                  </GameButton>
                )}
              </div>
            </div>
          )}
        </div>

        <GameTabs
          tabs={view.tabs.map((key) => ({ key, label: t(`technologies:tab.${key}`, { defaultValue: key }) }))}
          value={activeTab}
          onChange={setTab}
        />

        <div className="-mt-3 space-y-6 rounded-b-xl bg-[#1d3b6f] px-2 py-4">
          {view.tiers.map((tier) => (
            <TierTree
              key={tier.tier}
              nodes={view.nodes}
              tier={tier.tier}
              tab={activeTab}
              open={tier.open}
              lockedHint={t("technologies:page.tierLocked", { level: tier.mainBuildingLevel, worldLevel: tier.worldLevel })}
              activeKey={active === null ? null : `${active.tier}:${active.node}`}
              onPick={(node) => setPicked({ tier: node.tier, node: node.node })}
            />
          ))}
        </div>

        <section className="rounded-xl bg-white/10 p-3 text-sm">
          <h2 className="mb-1 font-semibold">{t("technologies:page.bonuses")}</h2>
          {view.bonuses.length === 0 ? (
            <p className="text-white/70">{t("technologies:page.noBonuses")}</p>
          ) : (
            <ul className="grid gap-x-4 gap-y-0.5 sm:grid-cols-2">
              {view.bonuses.map((bonus) => (
                <li key={`${bonus.stat}:${bonus.target ?? ""}`} className="flex justify-between gap-2">
                  <span className="text-white/80">{effectLabel(bonus, catalog)}</span>
                  <span className="font-semibold">{effectValue(bonus.stat, bonus.value)}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {selected !== null && (
        <ResearchNodeDialog
          node={selected}
          busy={active !== null}
          pending={start.isPending}
          onStart={() => start.mutate({ tier: selected.tier, node: selected.node }, { onSuccess: () => setPicked(null) })}
          onClose={() => setPicked(null)}
        />
      )}
    </GameScreen>
  );
}

function Progress({ startedAt, completesAt, now }: { startedAt: string; completesAt: string; now: number }) {
  const from = new Date(startedAt).getTime();
  const to = new Date(completesAt).getTime();
  const share = to <= from ? 1 : Math.min(1, Math.max(0, (now - from) / (to - from)));

  return (
    <div className="h-2 overflow-hidden rounded-full bg-black/30">
      <div className="h-full rounded-full bg-gradient-to-r from-emerald-400 to-lime-300" style={{ width: `${share * 100}%` }} />
    </div>
  );
}
