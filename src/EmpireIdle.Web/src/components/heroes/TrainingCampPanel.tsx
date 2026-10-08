import { useState } from "react";
import { useNow } from "../../hooks/useNow";
import { useCatalog } from "../../lib/queries/catalog";
import type { HeroesOverview, HeroSummary } from "../../lib/queries/heroes";
import { formatRemaining } from "../../lib/time";
import HeroPortrait from "./HeroPortrait";

interface Props {
  camp: HeroesOverview["camp"];
  heroes: HeroSummary[];
  busy: boolean;
  onPlace: (heroId: string, slot: number) => void;
  onRemove: (heroId: string) => void;
  onSkipCooldown: (slot: number) => void;
  onBuySlot: () => void;
}

/**
 * Вкладка «Табір» (GDD §6.1): опорна п'ятірка — найсильніші герої поза табором — і слоти.
 * Натискаєш вільний слот — обираєш будь-якого героя поза табором; якщо він був у п'ятірці,
 * його місце займає наступний за силою.
 */
export default function TrainingCampPanel({ camp, heroes, busy, onPlace, onRemove, onSkipCooldown, onBuySlot }: Props) {
  const catalog = useCatalog();
  const now = useNow();
  const [pickingSlot, setPickingSlot] = useState<number | null>(null);

  const byId = new Map(heroes.map((hero) => [hero.id, hero]));
  const reference = camp.referenceHeroIds.map((id) => byId.get(id)).filter((hero) => hero !== undefined);
  const candidates = heroes
    .filter((hero) => hero.campSlot == null)
    .sort((a, b) => b.level - a.level);

  const pick = (heroId: string) => {
    if (pickingSlot === null) return;
    onPlace(heroId, pickingSlot);
    setPickingSlot(null);
  };

  return (
    <div className="space-y-4">
      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Опорна п'ятірка</h2>
        <p className="text-sm text-slate-600">
          {camp.available
            ? `Герої в таборі отримують рівень ${camp.level} — найслабшого з ${camp.referenceSize} найсильніших героїв поза табором.`
            : `Табір запрацює, коли поза ним буде ${camp.referenceSize} героїв.`}
        </p>
        <ul className="flex flex-wrap gap-2">
          {reference.map((hero) => {
            const config = catalog.hero(hero.heroKey);

            return (
              <li key={hero.id} className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-2 py-1 text-sm">
                <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={32} />
                <span className="text-slate-800">{catalog.heroName(hero.heroKey)}</span>
                <span className="text-xs text-slate-500">рів. {hero.level}</span>
              </li>
            );
          })}
        </ul>
      </section>

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Слоти</h2>
        <ul className="grid gap-2 sm:grid-cols-2">
          {camp.slots.map((slot) => {
            const hero = slot.heroId == null ? null : (byId.get(slot.heroId) ?? null);
            const cooling = slot.cooldownUntil != null && new Date(slot.cooldownUntil).getTime() > now;

            return (
              <li
                key={slot.index}
                className={`flex items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm ${
                  pickingSlot === slot.index ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white"
                }`}
              >
                <span className="text-slate-500">#{slot.index + 1}</span>
                {hero !== null ? (
                  <>
                    <span className="flex-1 truncate text-slate-800">
                      {catalog.heroName(hero.heroKey)} · рів. {hero.effectiveLevel}
                      {hero.effectiveLevel !== hero.level && (
                        <span className="text-xs text-slate-400"> (власний {hero.level})</span>
                      )}
                    </span>
                    <button
                      type="button"
                      onClick={() => onRemove(hero.id)}
                      disabled={busy}
                      className="rounded border border-slate-300 px-2 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                    >
                      Вийняти
                    </button>
                  </>
                ) : cooling ? (
                  <>
                    <span className="flex-1 text-slate-500">перезарядка {formatRemaining(slot.cooldownUntil!, now)}</span>
                    <button
                      type="button"
                      onClick={() => onSkipCooldown(slot.index)}
                      disabled={busy}
                      className="rounded border border-violet-300 px-2 text-xs text-violet-700 hover:bg-violet-50 disabled:opacity-50"
                    >
                      {camp.skipCooldownGems} 💎
                    </button>
                  </>
                ) : (
                  <button
                    type="button"
                    onClick={() => setPickingSlot(pickingSlot === slot.index ? null : slot.index)}
                    disabled={busy || !camp.available}
                    className="flex-1 text-left text-emerald-700 hover:underline disabled:text-slate-400 disabled:no-underline"
                  >
                    {pickingSlot === slot.index ? "оберіть героя нижче" : "вільний — поставити героя"}
                  </button>
                )}
              </li>
            );
          })}
        </ul>

        <p className="text-xs text-slate-500">
          Слотів: {camp.totalSlots} (безкоштовних {camp.freeSlots}, куплених {camp.purchasedSlots} з{" "}
          {camp.maxPurchasableSlots}). Безкоштовні відкриваються з рівнем ратуші.
        </p>

        {camp.nextSlotPriceGems != null && (
          <button
            type="button"
            onClick={onBuySlot}
            disabled={busy}
            className="rounded-lg border border-violet-300 px-3 py-1 text-sm text-violet-700 hover:bg-violet-50 disabled:opacity-50"
          >
            Ще слот · {camp.nextSlotPriceGems} 💎
          </button>
        )}
      </section>

      {pickingSlot !== null && (
        <section className="space-y-2">
          <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Хто йде в слот #{pickingSlot + 1}</h2>
          <ul className="grid gap-2 sm:grid-cols-2">
            {candidates.map((hero) => {
              const config = catalog.hero(hero.heroKey);
              const inFive = camp.referenceHeroIds.includes(hero.id);

              return (
                <li key={hero.id}>
                  <button
                    type="button"
                    onClick={() => pick(hero.id)}
                    disabled={busy}
                    className="flex w-full items-center gap-2 rounded-lg border border-slate-200 bg-white px-2 py-1 text-left text-sm hover:border-emerald-400 disabled:opacity-50"
                  >
                    <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={32} />
                    <span className="flex-1 truncate text-slate-800">{catalog.heroName(hero.heroKey)}</span>
                    <span className="text-xs text-slate-500">
                      рів. {hero.level}
                      {inFive && " · з п'ятірки"}
                    </span>
                  </button>
                </li>
              );
            })}
          </ul>
        </section>
      )}
    </div>
  );
}
