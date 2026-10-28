import ErrorBanner from "../../ErrorBanner";
import type { InventoryItemResponse } from "../../../lib/apiTypes";
import { useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import { useBuyWeaponShards, useOpenWeaponChests, useUpgradeWeapon, type HeroSummary } from "../../../lib/queries/heroes";
import { useWallet } from "../../../lib/queries/wallet";
import { GameButton } from "../../game/GameUi";
import { formatNumber } from "../../../lib/format";
import { t } from "i18next";

/** Іконка зброї за класом героя: окремого предмета-зброї немає, тож і арту за ключем теж. */
const WEAPON_ICON: Record<string, string> = { warrior: "⚔️", knight: "🔱", archer: "🏹" };

interface SlotProps {
  hero: HeroSummary;
  config: CatalogHero;
  selected: boolean;
  onSelect: () => void;
}

/** Слот зброї під портретом: зброя — частина героя (GDD §6.4), її не знімають і не передають. */
export function WeaponSlot({ hero, config, selected, onSelect }: SlotProps) {
  const max = config.weaponBonusPercents.length;

  return (
    <button
      type="button"
      onClick={onSelect}
      title={config.weaponName ?? t("heroes:weapon.short")}
      className={`flex w-24 flex-col items-center rounded-xl border-2 px-2 py-1.5 text-xs ${
        hero.weaponLevel > 0 ? "border-amber-400 bg-amber-900/40" : "border-dashed border-sky-300/50 bg-[#16305e]/60"
      } ${selected ? "outline outline-3 outline-offset-2 outline-yellow-300" : ""}`}
    >
      <span className="text-2xl">{WEAPON_ICON[config.class] ?? "⚔️"}</span>
      <span className="font-semibold text-amber-100">{hero.weaponLevel > 0 ? `+${hero.weaponLevel}` : t("heroes:weapon.short")}</span>
      {hero.nextWeaponCost != null && (
        <span className={hero.weaponShards >= hero.nextWeaponCost ? "text-emerald-300" : "text-sky-200"}>
          {hero.weaponShards}/{hero.nextWeaponCost}
        </span>
      )}
      {hero.nextWeaponCost == null && hero.weaponLevel >= max && <span className="text-amber-200">{t("heroes:weapon.max")}</span>}
    </button>
  );
}

interface DetailsProps {
  playerId: string;
  hero: HeroSummary;
  config: CatalogHero;
  /** Стакові предмети рюкзака — звідти беруться скрині зброї. */
  items: InventoryItemResponse[];
}

/**
 * Зброя героя (GDD §6.4): рівень +1…+5 за шматки, бонус до всіх статів героя у відсотках.
 * Шматки лежать на герої: зі скринь зброї (трійка банера) або, у звичайних і рідкісних, — за gems.
 */
export function WeaponDetails({ playerId, hero, config, items }: DetailsProps) {
  const catalog = useCatalog();
  const wallet = useWallet(playerId);
  const upgrade = useUpgradeWeapon(playerId);
  const openChests = useOpenWeaponChests(playerId);
  const buyShards = useBuyWeaponShards(playerId);

  const busy = upgrade.isPending || openChests.isPending || buyShards.isPending;
  const max = config.weaponBonusPercents.length;
  const bonus = hero.weaponLevel > 0 ? (config.weaponBonusPercents[hero.weaponLevel - 1] ?? 0) : 0;
  const nextBonus = config.weaponBonusPercents[hero.weaponLevel] ?? null;
  const cost = hero.nextWeaponCost;
  const missing = cost == null ? 0 : Math.max(0, cost - hero.weaponShards);

  // Скриня зброї відкривається лише для героя зі своєї трійки
  const chests = items.filter(
    (item) => item.type === "weaponchest" && (catalog.item(item.itemKey)?.weaponHeroes ?? []).includes(hero.heroKey),
  );
  const shardPrice = config.weaponShardPriceGems ?? null;
  const gems = wallet.data?.gemBalance ?? 0;

  return (
    <div className="space-y-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
      <div>
        <p className="font-semibold">
          {config.weaponName ?? t("heroes:weapon.title")}
          <span className="text-amber-200"> {t("heroes:weapon.level", { level: hero.weaponLevel, max })}</span>
        </p>
        <p className="text-sm text-sky-100/90">
          {hero.weaponLevel > 0 ? t("heroes:weapon.bonus", { bonus }) : t("heroes:weapon.notForged")}
          {nextBonus !== null && <span className="text-emerald-300"> {t("heroes:weapon.next", { level: hero.weaponLevel + 1, bonus: nextBonus })}</span>}
        </p>
      </div>

      {cost == null ? (
        <p className="text-sm text-amber-200">{t("heroes:weapon.maxed")}</p>
      ) : (
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-sm">
            {t("heroes:weapon.pieces", { have: hero.weaponShards, cost })}
          </span>
          <GameButton onClick={() => upgrade.mutate(hero.id)} disabled={busy || missing > 0}>
            {t("heroes:weapon.upgrade", { level: hero.weaponLevel + 1 })}
          </GameButton>
        </div>
      )}

      {cost != null && missing > 0 && (
        <div className="flex flex-wrap gap-2">
          {chests.map((chest) => (
            <GameButton
              key={chest.itemKey}
              variant="secondary"
              onClick={() =>
                openChests.mutate({ heroId: hero.id, itemKey: chest.itemKey, count: Math.min(chest.count, missing) })
              }
              disabled={busy}
            >
              {t("heroes:weapon.openChests", { chest: chest.displayName, count: Math.min(chest.count, missing), have: chest.count })}
            </GameButton>
          ))}
          {shardPrice !== null && (
            <GameButton
              variant="accent"
              onClick={() => buyShards.mutate({ heroId: hero.id, count: missing })}
              disabled={busy || gems < shardPrice * missing}
            >
              {t("heroes:weapon.buy", { count: missing, price: formatNumber(shardPrice * missing) })}
            </GameButton>
          )}
          {chests.length === 0 && shardPrice === null && (
            <p className="text-sm text-sky-100/80">{t("heroes:weapon.onlyChests")}</p>
          )}
        </div>
      )}

      <ErrorBanner error={upgrade.error ?? openChests.error ?? buyShards.error} />
    </div>
  );
}
