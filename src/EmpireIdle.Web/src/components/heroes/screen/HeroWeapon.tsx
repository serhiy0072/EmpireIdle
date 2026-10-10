import ErrorBanner from "../../ErrorBanner";
import type { InventoryItemResponse } from "../../../lib/apiTypes";
import { useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import { useBuyWeaponShards, useOpenWeaponChests, useUpgradeWeapon, type HeroSummary } from "../../../lib/queries/heroes";
import { useWallet } from "../../../lib/queries/wallet";
import { GameButton } from "../../game/GameUi";

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
      title={config.weaponName ?? "Зброя"}
      className={`flex w-24 flex-col items-center rounded-xl border-2 px-2 py-1.5 text-xs ${
        hero.weaponLevel > 0 ? "border-amber-400 bg-amber-900/40" : "border-dashed border-sky-300/50 bg-[#16305e]/60"
      } ${selected ? "outline outline-3 outline-offset-2 outline-yellow-300" : ""}`}
    >
      <span className="text-2xl">{WEAPON_ICON[config.class] ?? "⚔️"}</span>
      <span className="font-semibold text-amber-100">{hero.weaponLevel > 0 ? `+${hero.weaponLevel}` : "Зброя"}</span>
      {hero.nextWeaponCost != null && (
        <span className={hero.weaponShards >= hero.nextWeaponCost ? "text-emerald-300" : "text-sky-200"}>
          {hero.weaponShards}/{hero.nextWeaponCost}
        </span>
      )}
      {hero.nextWeaponCost == null && hero.weaponLevel >= max && <span className="text-amber-200">макс.</span>}
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
          {config.weaponName ?? "Зброя героя"}
          <span className="text-amber-200"> · +{hero.weaponLevel} з {max}</span>
        </p>
        <p className="text-sm text-sky-100/90">
          {hero.weaponLevel > 0 ? `Усі стати героя +${bonus}%` : "Ще не викувана — перші шматки відкривають +1."}
          {nextBonus !== null && <span className="text-emerald-300"> · на +{hero.weaponLevel + 1}: +{nextBonus}%</span>}
        </p>
      </div>

      {cost == null ? (
        <p className="text-sm text-amber-200">Зброя на найвищому рівні.</p>
      ) : (
        <div className="flex flex-wrap items-center gap-3">
          <span className="text-sm">
            Шматки: {hero.weaponShards} / {cost}
          </span>
          <GameButton onClick={() => upgrade.mutate(hero.id)} disabled={busy || missing > 0}>
            Покращити до +{hero.weaponLevel + 1}
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
              {chest.displayName}: відкрити {Math.min(chest.count, missing)} (є {chest.count})
            </GameButton>
          ))}
          {shardPrice !== null && (
            <GameButton
              variant="accent"
              onClick={() => buyShards.mutate({ heroId: hero.id, count: missing })}
              disabled={busy || gems < shardPrice * missing}
            >
              Докупити {missing} за {(shardPrice * missing).toLocaleString("uk-UA")} 💎
            </GameButton>
          )}
          {chests.length === 0 && shardPrice === null && (
            <p className="text-sm text-sky-100/80">Шматки унікальної зброї — лише зі скринь зброї в крамниці.</p>
          )}
        </div>
      )}

      <ErrorBanner error={upgrade.error ?? openChests.error ?? buyShards.error} />
    </div>
  );
}
