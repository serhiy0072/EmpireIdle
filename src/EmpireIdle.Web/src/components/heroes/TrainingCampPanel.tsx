import { useState } from "react";
import { useNow } from "../../hooks/useNow";
import { useCatalog } from "../../lib/queries/catalog";
import type { HeroesOverview, HeroSummary } from "../../lib/queries/heroes";
import { formatRemaining } from "../../lib/time";
import { GameButton, RoleBadge, StarRow, Tile, TileGrid } from "../game/GameUi";
import HeroPortrait from "./HeroPortrait";
import { t } from "i18next";
import { Trans } from "react-i18next";

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
        top={<span className="pl-4">{t("heroes:list.level", { level })}</span>}
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
            {key === "train" ? t("heroes:camp.tabTrain") : t("heroes:camp.tabReset")}
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
                    <span className="rounded bg-slate-900/60 px-2 text-xs font-semibold">{t("heroes:list.level", { level: hero.level })}</span>
                  </div>
                );
              })}
            </div>
            <p className="rounded-lg bg-slate-900/40 px-3 py-2 text-center text-sm">
              {camp.available ? (
                <Trans
                  ns="heroes"
                  i18nKey="camp.intro"
                  values={{ count: camp.referenceSize, level: camp.level }}
                  components={{ hl: <span className="text-amber-300" />, b: <span className="font-bold text-amber-300" /> }}
                />
              ) : (
                t("heroes:camp.unavailable", { count: camp.referenceSize })
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
                    title={t("heroes:camp.skipCooldown")}
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
                  title={t("heroes:camp.place")}
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
                title={t("heroes:camp.buySlot")}
                className="flex aspect-square w-full flex-col items-center justify-center rounded-xl border-2 border-dashed border-violet-300/60 text-xs text-violet-200 disabled:opacity-50"
              >
                <span className="text-2xl">🔒</span>
                {camp.nextSlotPriceGems} 💎
              </button>
            )}
          </TileGrid>

          <p className="text-center text-xs text-sky-200/70">
            {t("heroes:camp.slots", {
              total: camp.totalSlots,
              free: camp.freeSlots,
              bought: camp.purchasedSlots,
              max: camp.maxPurchasableSlots,
            })}
          </p>

          {focusedHero !== null && (
            <div className="flex items-center justify-between gap-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
              <span className="text-sm">
                {t("heroes:camp.focused", { hero: catalog.heroName(focusedHero.heroKey), level: focusedHero.effectiveLevel })}
                {focusedHero.effectiveLevel !== focusedHero.level && (
                  <span className="text-sky-200/80"> {t("heroes:camp.ownLevel", { level: focusedHero.level })}</span>
                )}
              </span>
              <GameButton
                onClick={() => {
                  onRemove(focusedHero.id);
                  setFocused(null);
                }}
                disabled={busy}
                variant="secondary"
              >
                {t("heroes:camp.remove")}
              </GameButton>
            </div>
          )}

          {pickingSlot !== null && (
            <section className="space-y-2">
              <h3 className="text-sm font-semibold">{t("heroes:camp.pick", { slot: pickingSlot + 1 })}</h3>
              <TileGrid>
                {/* Інструкторів у табір не ставлять (рішення 13.10.2026) — пропонуємо лише слабших за п'ятірку */}
                {outside
                  .filter((hero) => !camp.referenceHeroIds.includes(hero.id))
                  .map((hero) =>
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
              <p className="text-xs text-sky-200/70">
                {t("heroes:camp.pickHint")}
              </p>
            </section>
          )}
        </>
      ) : (
        <>
          <p className="rounded-lg bg-slate-900/40 px-3 py-2 text-center text-sm">
            <Trans ns="heroes" i18nKey="camp.resetIntro" components={{ hl: <span className="text-amber-300" /> }} />
          </p>
          <TileGrid>
            {outside
              .filter((hero) => hero.level > 1)
              .map((hero) => heroTile(hero, () => setFocused(focused === hero.id ? null : hero.id), focused === hero.id, hero.level))}
          </TileGrid>

          {focusedHero !== null && (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
              <span className="text-sm">
                {t("heroes:camp.resetConfirm", { hero: catalog.heroName(focusedHero.heroKey), level: focusedHero.level })}
              </span>
              <GameButton
                onClick={() => {
                  onResetLevel(focusedHero.id);
                  setFocused(null);
                }}
                disabled={busy}
                variant="accent"
              >
                {t("heroes:camp.reset")}
              </GameButton>
            </div>
          )}
        </>
      )}
    </div>
  );
}
