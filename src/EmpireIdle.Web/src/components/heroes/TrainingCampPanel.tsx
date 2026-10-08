import { useState } from "react";
import { useNow } from "../../hooks/useNow";
import { useCatalog } from "../../lib/queries/catalog";
import type { HeroesOverview, HeroSummary } from "../../lib/queries/heroes";
import { formatRemaining } from "../../lib/time";
import { GameButton, RoleBadge, StarRow, Tile, TileGrid } from "../game/GameUi";
import HeroPortrait from "./HeroPortrait";

interface Props {
  camp: HeroesOverview["camp"];
  heroes: HeroSummary[];
  busy: boolean;
  onPlace: (heroId: string, slot: number) => void;
  onRemove: (heroId: string) => void;
  onSkipCooldown: (slot: number) => void;
  onBuySlot: () => void;
  onResetLevel: (heroId: string) => void;
}

type Mode = "train" | "reset";

/**
 * Навчальний табір (GDD §6.1, за референсом 08.10.2026): угорі п'ятірка «інструкторів» — найсильніші
 * поза табором, нижче слоти плитками. Друга вкладка — скидання рівня: досвід повертається в пул.
 */
export default function TrainingCampPanel({ camp, heroes, busy, onPlace, onRemove, onSkipCooldown, onBuySlot, onResetLevel }: Props) {
  const catalog = useCatalog();
  const now = useNow();
  const [mode, setMode] = useState<Mode>("train");
  const [pickingSlot, setPickingSlot] = useState<number | null>(null);
  const [focused, setFocused] = useState<string | null>(null);

  const byId = new Map(heroes.map((hero) => [hero.id, hero]));
  const reference = camp.referenceHeroIds.map((id) => byId.get(id)).filter((hero) => hero !== undefined);
  const outside = heroes.filter((hero) => hero.campSlot == null).sort((a, b) => b.level - a.level);
  const focusedHero = focused === null ? null : (byId.get(focused) ?? null);

  const heroTile = (hero: HeroSummary, onClick: () => void, selected: boolean, level = hero.effectiveLevel) => {
    const config = catalog.hero(hero.heroKey);
  
    return (
      <Tile
        key={hero.id}
        rarity={config?.rank}
        onClick={onClick}
        selected={selected}
        dimmed={busy}
        title={catalog.heroName(hero.heroKey)}
        corner={<RoleBadge role={config?.class} size={16} />}
        top={<span className="pl-4">Ур. {level}</span>}
        bottom={<StarRow starParts={hero.starParts} partsPerStar={catalog.partsPerStar} maxStars={catalog.maxStars} size={11} />}
      >
        <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={64} />
      </Tile>
    );
  };

  return (
    <div className="space-y-4">
      <div className="mx-auto flex max-w-md rounded-xl bg-[#16305e] p-1 ring-1 ring-[#335c9a]">
        {(["train", "reset"] as const).map((key) => (
          <button
            key={key}
            type="button"
            onClick={() => {
              setMode(key);
              setFocused(null);
              setPickingSlot(null);
            }}
            className={`flex-1 rounded-lg py-1.5 text-sm font-semibold ${mode === key ? "bg-sky-500 text-white shadow" : "text-sky-200"}`}
          >
            {key === "train" ? "Навчання на рівень" : "Скидання рівня"}
          </button>
        ))}
      </div>

      {mode === "train" ? (
        <>
          <section className="space-y-2">
            <div className="flex justify-center gap-3">
              {reference.map((hero) => {
                const config = catalog.hero(hero.heroKey);

                return (
                  <div key={hero.id} className="flex flex-col items-center gap-1">
                    <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={56} />
                    <span className="rounded bg-slate-900/60 px-2 text-xs font-semibold">Ур. {hero.level}</span>
                  </div>
                );
              })}
            </div>
            <p className="rounded-lg bg-slate-900/40 px-3 py-2 text-center text-sm">
              {camp.available ? (
                <>
                  Герої на місці навчання автоматично підтягуються до{" "}
                  <span className="text-amber-300">мінімального рівня {camp.referenceSize} інструкторів</span> — зараз це{" "}
                  <span className="font-bold text-amber-300">{camp.level}</span>.
                </>
              ) : (
                `Табір запрацює, коли поза ним буде ${camp.referenceSize} героїв.`
              )}
            </p>
          </section>

          <TileGrid>
            {camp.slots.map((slot) => {
              const hero = slot.heroId == null ? null : (byId.get(slot.heroId) ?? null);
              const cooling = slot.cooldownUntil != null && new Date(slot.cooldownUntil).getTime() > now;

              if (hero !== null)
                return heroTile(hero, () => setFocused(focused === hero.id ? null : hero.id), focused === hero.id);

              if (cooling)
                return (
                  <button
                    key={slot.index}
                    type="button"
                    onClick={() => onSkipCooldown(slot.index)}
                    disabled={busy}
                    title="Пропустити перезарядку"
                    className="flex aspect-square w-full flex-col items-center justify-center rounded-xl bg-slate-800/80 text-xs text-sky-100 ring-1 ring-slate-500 disabled:opacity-50"
                  >
                    ⏳ {formatRemaining(slot.cooldownUntil!, now)}
                    <span className="mt-1 text-violet-300">{camp.skipCooldownGems} 💎</span>
                  </button>
                );

              return (
                <button
                  key={slot.index}
                  type="button"
                  onClick={() => setPickingSlot(pickingSlot === slot.index ? null : slot.index)}
                  disabled={busy || !camp.available}
                  title="Поставити героя"
                  className={`flex aspect-square w-full items-center justify-center rounded-xl bg-[#16305e] text-4xl text-white ring-1 ring-[#335c9a] hover:bg-[#1f3d75] disabled:opacity-40 ${
                    pickingSlot === slot.index ? "outline outline-3 outline-offset-2 outline-yellow-300" : ""
                  }`}
                >
                  +
                </button>
              );
            })}

            {camp.nextSlotPriceGems != null && (
              <button
                type="button"
                onClick={onBuySlot}
                disabled={busy}
                title="Купити ще слот"
                className="flex aspect-square w-full flex-col items-center justify-center rounded-xl border-2 border-dashed border-violet-300/60 text-xs text-violet-200 disabled:opacity-50"
              >
                <span className="text-2xl">🔒</span>
                {camp.nextSlotPriceGems} 💎
              </button>
            )}
          </TileGrid>

          <p className="text-center text-xs text-sky-200/70">
            Слотів: {camp.totalSlots} (безкоштовних {camp.freeSlots}, куплених {camp.purchasedSlots} з {camp.maxPurchasableSlots}).
            Безкоштовні відкриваються з рівнем ратуші.
          </p>

          {focusedHero !== null && (
            <div className="flex items-center justify-between gap-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
              <span className="text-sm">
                {catalog.heroName(focusedHero.heroKey)} · рівень {focusedHero.effectiveLevel}
                {focusedHero.effectiveLevel !== focusedHero.level && <span className="text-sky-200/80"> (власний {focusedHero.level})</span>}
              </span>
              <GameButton
                onClick={() => {
                  onRemove(focusedHero.id);
                  setFocused(null);
                }}
                disabled={busy}
                variant="secondary"
              >
                Вийняти з табору
              </GameButton>
            </div>
          )}

          {pickingSlot !== null && (
            <section className="space-y-2">
              <h3 className="text-sm font-semibold">Хто йде в слот #{pickingSlot + 1}</h3>
              <TileGrid>
                {outside.map((hero) =>
                  heroTile(
                    hero,
                    () => {
                      onPlace(hero.id, pickingSlot);
                      setPickingSlot(null);
                    },
                    false,
                    hero.level,
                  ),
                )}
              </TileGrid>
              <p className="text-xs text-sky-200/70">Якщо герой був серед інструкторів, його місце займає наступний за силою.</p>
            </section>
          )}
        </>
      ) : (
        <>
          <p className="rounded-lg bg-slate-900/40 px-3 py-2 text-center text-sm">
            Скидання повертає героя на 1 рівень, а <span className="text-amber-300">увесь витрачений досвід — у загальний запас</span>.
          </p>
          <TileGrid>
            {outside
              .filter((hero) => hero.level > 1)
              .map((hero) => heroTile(hero, () => setFocused(focused === hero.id ? null : hero.id), focused === hero.id, hero.level))}
          </TileGrid>

          {focusedHero !== null && (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
              <span className="text-sm">
                Скинути {catalog.heroName(focusedHero.heroKey)} з {focusedHero.level} на 1 рівень?
              </span>
              <GameButton
                onClick={() => {
                  onResetLevel(focusedHero.id);
                  setFocused(null);
                }}
                disabled={busy}
                variant="accent"
              >
                Скинути рівень
              </GameButton>
            </div>
          )}
        </>
      )}
    </div>
  );
}
