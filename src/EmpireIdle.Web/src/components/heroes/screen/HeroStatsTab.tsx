import { useState } from "react";
import { heroState, useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { statLabel } from "../../../lib/statNames";
import { GameButton } from "../../game/GameUi";

interface Props {
  hero: HeroSummary;
  config: CatalogHero;
  busy: boolean;
  onEvolve: () => void;
  onAppointLeader: () => void;
  onHeal: () => void;
  onResetLevel: () => void;
}

/**
 * Вкладка «Показники»: стан героя, базові стати з довідника, тір і рідші дії — еволюція,
 * лідерство, лікування, скидання рівня. Те, що качається щодня (рівень, зірки), — над вкладками.
 */
export default function HeroStatsTab({ hero, config, busy, onEvolve, onAppointLeader, onHeal, onResetLevel }: Props) {
  const catalog = useCatalog();
  const [confirmingReset, setConfirmingReset] = useState(false);

  const atTierCap = hero.tier >= catalog.maxTier;
  const stationed = hero.stationedGarrisonId != null;

  return (
    <div className="space-y-4 text-sm">
      <dl className="grid grid-cols-2 gap-x-4 gap-y-1 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a] sm:grid-cols-3">
        <Row label="Стан" value={heroState(hero.state)} />
        <Row label="Тір" value={`${hero.tier} з ${catalog.maxTier}`} />
        <Row label="Рівень" value={`${hero.effectiveLevel} з ${hero.maxLevel}${hero.campSlot != null && hero.effectiveLevel !== hero.level ? ` (власний ${hero.level}, табір)` : ""}`} />
        <Row label="Веде" value={hero.unitType === null || hero.unitType === undefined ? "—" : catalog.unitName(hero.unitType)} />
        <Row label="Швидкість" value={String(config.speed)} />
        <Row label="Лідер гарнізону" value={hero.isLeader ? "так" : "ні"} />
        {Object.entries(config.baseStats).map(([stat, value]) => (
          <Row key={stat} label={`${statLabel(stat)} (база)`} value={`${value} +${config.statGrowth[stat] ?? 0}/рів.`} />
        ))}
      </dl>

      {config.description !== "" && <p className="text-sky-50/90">{config.description}</p>}

      {/* Історію розгортають на вимогу, повністю вона — у кодексі */}
      {config.lore !== null && config.lore !== undefined && config.lore !== "" && (
        <details>
          <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wide text-sky-200">Історія</summary>
          <p className="mt-1 border-l-2 border-amber-300 pl-3 italic leading-relaxed text-sky-50/90">{config.lore}</p>
        </details>
      )}

      <div className="flex flex-wrap justify-center gap-2">
        <GameButton onClick={onEvolve} disabled={busy || atTierCap} variant="accent">
          {atTierCap ? "Максимальний тір" : "Еволюція тіру"}
        </GameButton>

        {stationed && !hero.isLeader && (
          <GameButton onClick={onAppointLeader} disabled={busy}>
            Призначити лідером
          </GameButton>
        )}

        {hero.state === "Wounded" && (
          <GameButton onClick={onHeal} disabled={busy} variant="accent">
            Вилікувати
          </GameButton>
        )}

        {/* Скидання не скасувати — тож другий клік; досвід повертається в пул повністю */}
        {hero.level > 1 && !confirmingReset && (
          <GameButton onClick={() => setConfirmingReset(true)} disabled={busy} variant="secondary">
            Скинути рівень
          </GameButton>
        )}
      </div>

      {confirmingReset && (
        <div className="flex flex-wrap items-center justify-center gap-2">
          <span className="text-sky-50">Скинути на 1 рівень? Увесь досвід повернеться в пул.</span>
          <GameButton
            onClick={() => {
              setConfirmingReset(false);
              onResetLevel();
            }}
            disabled={busy}
            variant="accent"
          >
            Так
          </GameButton>
          <GameButton onClick={() => setConfirmingReset(false)} variant="secondary">
            Ні
          </GameButton>
        </div>
      )}

      <p className="text-center text-xs text-sky-200/70">Стеля рівня — нижча з двох: рівень ратуші й тір × 10.</p>
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
