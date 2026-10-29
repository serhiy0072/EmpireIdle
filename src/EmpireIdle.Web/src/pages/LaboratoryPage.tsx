import { useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GameScreen, GameTabs } from "../components/game/GameUi";
import ResearchNodeDialog from "../components/research/ResearchNodeDialog";
import SpeedUpItemsButton from "../components/SpeedUpItemsButton";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { ResearchNodeView, ResearchView } from "../lib/apiTypes";
import { formatNumber } from "../lib/format";
import { useCatalog } from "../lib/queries/catalog";
import { useClanHelp, useMyClan, useRequestHelp } from "../lib/queries/clans";
import { useResearch, useSpeedUpResearch, useStartResearch } from "../lib/queries/research";
import { speedUpLabel } from "../lib/speedUp";
import { effectLabel, effectValue, technologyName, tierNumeral } from "../lib/technologies";
import { formatRemaining } from "../lib/time";
import { t } from "i18next";

/** ClanHelpTarget.Research на дроті. */
const HELP_TARGET_RESEARCH = 2;

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
                    onClick={() => requestHelp.mutate({ targetType: HELP_TARGET_RESEARCH, targetId: view.researchId! })}
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
              view={view}
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

interface TierTreeProps {
  view: ResearchView;
  tier: number;
  tab: string;
  open: boolean;
  lockedHint: string;
  activeKey: string | null;
  onPick: (node: ResearchNodeView) => void;
}

/** Один тір вкладки: ряди вузлів згори вниз, у ряду — поруч. */
function TierTree({ view, tier, tab, open, lockedHint, activeKey, onPick }: TierTreeProps) {
  const nodes = view.nodes.filter((node) => node.tier === tier && node.tab === tab);
  const rows = [...new Set(nodes.map((node) => node.row))].sort((a, b) => a - b);

  if (nodes.length === 0) return null;

  return (
    <section className={open ? "" : "opacity-60"}>
      <div className="mb-3 flex items-center gap-2">
        <span className="h-px flex-1 bg-sky-300/40" />
        <span className="text-sm font-semibold">{t("technologies:page.tier", { tier: tierNumeral(tier) })}</span>
        {!open && <span className="text-xs text-white/70">🔒 {lockedHint}</span>}
        <span className="h-px flex-1 bg-sky-300/40" />
      </div>
      <div className="space-y-4">
        {rows.map((row) => (
          <div key={row} className="flex flex-wrap justify-center gap-4">
            {nodes
              .filter((node) => node.row === row)
              .map((node) => (
                <NodeTile key={node.node} node={node} active={activeKey === `${node.tier}:${node.node}`} onClick={() => onPick(node)} />
              ))}
          </div>
        ))}
      </div>
    </section>
  );
}

function NodeTile({ node, active, onClick }: { node: ResearchNodeView; active: boolean; onClick: () => void }) {
  const maxed = node.level >= node.maxLevel;
  const available = node.availability === "Available";

  return (
    <button type="button" onClick={onClick} className="flex w-24 flex-col items-center gap-1 text-center">
      <span
        className={`relative flex h-16 w-16 items-center justify-center rounded-2xl text-2xl shadow ring-2 ${
          maxed
            ? "bg-gradient-to-b from-amber-300 to-orange-500 ring-amber-200"
            : available
              ? "bg-gradient-to-b from-sky-300 to-blue-600 ring-sky-200"
              : "bg-gradient-to-b from-slate-400 to-slate-600 ring-slate-300 grayscale"
        } ${active ? "animate-pulse" : ""}`}
      >
        {iconFor(node)}
        <span className="absolute -bottom-2 rounded-full bg-[#16305e] px-1.5 text-[10px] font-bold ring-1 ring-white/40">
          {maxed ? t("technologies:page.max") : `${formatNumber(node.level)}/${formatNumber(node.maxLevel)}`}
        </span>
      </span>
      <span className="mt-1 text-xs leading-tight">
        {technologyName(node.node)} {tierNumeral(node.tier)}
      </span>
    </button>
  );
}

/** Значок за першим ефектом вузла — поки без малюнків. */
function iconFor(node: ResearchNodeView): string {
  // Порядок — як у TechnologyStat: атака, захист, конвой, марш, вантаж, видобуток, їжа, будівництво…
  const icons = ["⚔️", "🛡️", "🚩", "🐎", "🛒", "⛏️", "🍖", "🏗️", "🎯", "⬆️", "📜", "🩹", "🏕️", "⭐", "🐾", "🔐"];
  const stat = node.effects[0]?.stat;

  return stat === undefined ? "🔬" : (icons[stat] ?? "🔬");
}
