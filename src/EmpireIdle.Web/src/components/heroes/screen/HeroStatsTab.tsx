import { useState } from "react";
import { heroState, useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { statLabel, statValue } from "../../../lib/statNames";
import { GameButton } from "../../game/GameUi";
import { t } from "i18next";

interface Props {
  hero: HeroSummary;
  config: CatalogHero;
  busy: boolean;
  onEvolve: () => void;
  onHeal: () => void;
  onResetLevel: () => void;
}

/**
 * Вкладка «Показники»: стан героя, базові стати з довідника, тір і рідші дії — еволюція,
 * лікування, скидання рівня. Те, що качається щодня (рівень, зірки), — над вкладками.
 */
export default function HeroStatsTab({ hero, config, busy, onEvolve, onHeal, onResetLevel }: Props) {
  const catalog = useCatalog();
  const [confirmingReset, setConfirmingReset] = useState(false);

  const atTierCap = hero.tier >= catalog.maxTier;

  return (
    <div className="space-y-4 text-sm">
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a] sm:grid-cols-3">
        <Row label={t("heroes:stats.state")} value={heroState(hero.state)} />
        <Row label={t("heroes:stats.tier")} value={t("heroes:stats.ofMax", { value: hero.tier, max: catalog.maxTier })} />
        <Row
          label={t("heroes:stats.level")}
          value={`${t("heroes:stats.ofMax", { value: hero.effectiveLevel, max: hero.maxLevel })}${
            hero.campSlot != null && hero.effectiveLevel !== hero.level ? t("heroes:stats.campLevel", { level: hero.level }) : ""
          }`}
        />
        <Row label={t("heroes:stats.leads")} value={hero.unitType === null || hero.unitType === undefined ? "—" : catalog.unitName(hero.unitType)} />
        <Row label={t("heroes:stats.speed")} value={String(config.speed)} />
        {/* Боронить сам найсильніший свого класу — призначати нікого не треба, і марш не заважає */}
        <Row label={t("heroes:stats.defender")} value={hero.isDefender ? t("heroes:stats.yes") : t("heroes:stats.no")} />
      </dl>

      {/* Стати зараз — з тіром, зірками, зброєю й спорядженням; поруч — що дасть наступний рівень */}
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a] sm:grid-cols-3">
        {Object.entries(hero.stats).map(([stat, value]) => {
          const next = hero.nextLevelStats?.[stat];
          const gain = next === undefined ? 0 : next - value;

          return (
            <div key={stat}>
              <dt className="text-xs text-sky-200">{statLabel(stat)}</dt>
              <dd className="font-semibold">
                {statValue(stat, value)}
                {gain > 0.05 && (
                  <span className="ml-1 text-xs font-normal text-emerald-300">
                    → {statValue(stat, next ?? value)} (+{statValue(stat, gain)})
                  </span>
                )}
              </dd>
            </div>
          );
        })}
      </dl>
      {hero.nextLevelStats != null && (
        <p className="-mt-2 text-xs text-sky-200/80">{t("heroes:stats.preview", { level: hero.effectiveLevel + 1 })}</p>
      )}

      {config.description !== "" && <p className="text-sky-50/90">{config.description}</p>}

      {/* Історію розгортають на вимогу, повністю вона — у кодексі */}
      {config.lore !== null && config.lore !== undefined && config.lore !== "" && (
        <details>
          <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wide text-sky-200">{t("heroes:stats.lore")}</summary>
          <p className="mt-1 border-l-2 border-amber-300 pl-3 italic leading-relaxed text-sky-50/90">{config.lore}</p>
        </details>
      )}

      <div className="flex flex-wrap justify-center gap-2">
        <GameButton onClick={onEvolve} disabled={busy || atTierCap} variant="accent">
          {atTierCap ? t("heroes:stats.maxTier") : t("heroes:stats.evolve")}
        </GameButton>

        {hero.state === "Wounded" && (
          <GameButton onClick={onHeal} disabled={busy} variant="accent">
            {t("heroes:stats.heal")}
          </GameButton>
        )}

        {/* Скидання не скасувати — тож другий клік; досвід повертається в пул повністю */}
        {hero.level > 1 && !confirmingReset && (
          <GameButton onClick={() => setConfirmingReset(true)} disabled={busy} variant="secondary">
            {t("heroes:camp.reset")}
          </GameButton>
        )}
      </div>

      {confirmingReset && (
        <div className="flex flex-wrap items-center justify-center gap-2">
          <span className="text-sky-50">{t("heroes:stats.resetConfirm")}</span>
          <GameButton
            onClick={() => {
              setConfirmingReset(false);
              onResetLevel();
            }}
            disabled={busy}
            variant="accent"
          >
            {t("heroes:stats.confirmYes")}
          </GameButton>
          <GameButton onClick={() => setConfirmingReset(false)} variant="secondary">
            {t("heroes:stats.confirmNo")}
          </GameButton>
        </div>
      )}

      <p className="text-center text-xs text-sky-200/70">{t("heroes:stats.levelHint")}</p>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-2 sm:block">
      <dt className="text-sky-200/80">{label}</dt>
      <dd className="font-semibold">{value}</dd>
    </div>
  );
}
