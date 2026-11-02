import { useState } from "react";
import { Link } from "react-router-dom";
import type { InventoryItemResponse } from "../../../lib/apiTypes";
import { useCatalog } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { useUseItem } from "../../../lib/queries/inventory";
import { statGains } from "../../../lib/statNames";
import { GameButton } from "../../game/GameUi";
import ItemIcon from "../../inventory/ItemIcon";
import { formatNumber } from "../../../lib/format";
import { t } from "i18next";

/** Скриня крамниці, з якої падають банки досвіду. */
const JAR_CHEST = "chest_supply";

interface Props {
  playerId: string;
  hero: HeroSummary;
  /** Спільний запас досвіду гравця (GDD §6.1). */
  pool: number;
  items: InventoryItemResponse[];
  busy: boolean;
  onLevelUp: () => void;
}

/**
 * Підняття рівня героя: скільки досвіду є й скільки треба. Кнопка завжди активна: забракло — замість
 * відмови пропонуємо вжити банки з рюкзака, кожну цілим стеком, або піти по скриню в крамницю.
 */
export default function HeroLevelUp({ playerId, hero, pool, items, busy, onLevelUp }: Props) {
  const catalog = useCatalog();
  const drinkJar = useUseItem(playerId);
  const [offering, setOffering] = useState(false);

  const need = hero.experienceToNext;
  const affordable = pool >= need;
  const missing = Math.max(0, need - pool);

  // Від дрібних до великих: так видно, чи вистачить дрібних
  const jars = items
    .filter((item) => item.type === "heroxp")
    .map((item) => ({ item, experience: catalog.item(item.itemKey)?.heroExperience ?? 0 }))
    .sort((a, b) => a.experience - b.experience);

  const press = () => (affordable ? onLevelUp() : setOffering(true));

  // Рівень героя в таборі тримає табір — власний не піднімається, сервер однаково відмовить
  if (hero.campSlot != null) {
    return (
      <p className="max-w-md text-center text-sm text-amber-200">
        {t("heroes:levelUp.inCamp", { level: hero.effectiveLevel })}
      </p>
    );
  }

  return (
    <div className="flex w-full max-w-md flex-col items-center gap-2">
      <p className="text-sm text-sky-100">
        {t("heroes:levelUp.experience")}{" "}
        <span className={affordable ? "font-bold text-emerald-300" : "font-bold text-amber-300"}>{formatNumber(pool)}</span>
        {" / "}
        <span className="font-bold">{formatNumber(need)}</span>
      </p>

      <GameButton onClick={press} disabled={busy || drinkJar.isPending} variant="accent">
        {t("heroes:levelUp.raise")}
      </GameButton>

      {/* Що дасть рівень — до витрати досвіду */}
      {statGains(hero.stats, hero.nextLevelStats).length > 0 && (
        <p className="text-xs text-emerald-300">{statGains(hero.stats, hero.nextLevelStats).join(" · ")}</p>
      )}

      {!affordable && offering && (
        <div className="w-full space-y-2 rounded-xl bg-[#16305e]/80 p-3 text-left ring-1 ring-[#335c9a]">
          <p className="text-sm text-amber-200">{t("heroes:levelUp.missing", { count: formatNumber(missing) })}</p>

          {jars.length === 0 ? (
            <p className="text-sm text-sky-100/80">{t("heroes:levelUp.noJars")}</p>
          ) : (
            jars.map(({ item, experience }) => (
              <div key={item.itemKey} className="flex items-center gap-2">
                <ItemIcon itemKey={item.itemKey} type={item.type} rarity={item.rarity} size={32} />
                <span className="flex-1 text-sm">
                  {item.displayName} ×{formatNumber(item.count)}
                  {experience > 0 && (
                    <span className="text-sky-200/80"> · +{formatNumber(experience * item.count)}</span>
                  )}
                </span>
                <GameButton
                  variant="secondary"
                  onClick={() =>
                    drinkJar.mutate({ itemKey: item.itemKey, count: Math.min(item.count, 10_000), targetX: null, targetY: null })
                  }
                  disabled={busy || drinkJar.isPending}
                >
                  {t("heroes:levelUp.useAll")}
                </GameButton>
              </div>
            ))
          )}

          <Link to={`/shop#${JAR_CHEST}`} className="inline-block text-sm text-sky-300 hover:underline">
            {t("heroes:levelUp.shopLink", { chest: catalog.itemName(JAR_CHEST) })}
          </Link>
        </div>
      )}
    </div>
  );
}
