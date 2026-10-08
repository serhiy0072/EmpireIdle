import { useNow } from "../../hooks/useNow";
import { useCatalog } from "../../lib/queries/catalog";
import type { HeroesOverview } from "../../lib/queries/heroes";
import { formatRemaining } from "../../lib/time";

interface Props {
  camp: HeroesOverview["camp"];
  heroes: HeroesOverview["heroes"];
  busy: boolean;
  onRemove: (heroId: string) => void;
  onSkipCooldown: (slot: number) => void;
  onBuySlot: () => void;
}

/**
 * Навчальний табір (GDD §6.1): герой у слоті отримує рівень найслабшого з п'ятірки найсильніших
 * поза табором. Ставлять героя з його картки; тут — слоти, перезарядки й докупівля.
 */
export default function TrainingCampPanel({ camp, heroes, busy, onRemove, onSkipCooldown, onBuySlot }: Props) {
  const catalog = useCatalog();
  const now = useNow();

  return (
    <section className="space-y-2">
      <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Навчальний табір</h2>
      <p className="text-sm text-slate-600">
        {camp.available
          ? `Рівень табору ${camp.level} — найслабший із ${camp.referenceSize} найсильніших героїв поза табором.`
          : `Табір відкриється, коли поза ним буде ${camp.referenceSize} героїв.`}
      </p>

      <ul className="grid gap-2 sm:grid-cols-2">
        {camp.slots.map((slot) => {
          const hero = slot.heroId == null ? null : (heroes.find((h) => h.id === slot.heroId) ?? null);
          const cooling = slot.cooldownUntil != null && new Date(slot.cooldownUntil).getTime() > now;

          return (
            <li key={slot.index} className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm">
              <span className="text-slate-500">#{slot.index + 1}</span>
              {hero !== null ? (
                <>
                  <span className="flex-1 truncate text-slate-800">
                    {catalog.heroName(hero.heroKey)} · рів. {hero.effectiveLevel}
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
                <span className="flex-1 text-slate-400">вільний</span>
              )}
            </li>
          );
        })}
      </ul>

      <p className="text-xs text-slate-500">
        Слотів: {camp.totalSlots} (безкоштовних {camp.freeSlots}, куплених {camp.purchasedSlots} з {camp.maxPurchasableSlots}).
        Безкоштовні відкриваються з рівнем ратуші.
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
  );
}
