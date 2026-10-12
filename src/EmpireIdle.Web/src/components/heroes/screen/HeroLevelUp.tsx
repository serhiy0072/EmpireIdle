import { useState } from "react";
import { Link } from "react-router-dom";
import type { InventoryItemResponse } from "../../../lib/apiTypes";
import { useCatalog } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { useUseItem } from "../../../lib/queries/inventory";
import { statGains } from "../../../lib/statNames";
import { GameButton } from "../../game/GameUi";
import ItemIcon from "../../inventory/ItemIcon";

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
        Герой у навчальному таборі: рівень {hero.effectiveLevel} тримає табір. Виведіть героя з табору, щоб піднімати власний рівень.
      </p>
    );
  }

  return (
    <div className="flex w-full max-w-md flex-col items-center gap-2">
      <p className="text-sm text-sky-100">
        Досвід:{" "}
        <span className={affordable ? "font-bold text-emerald-300" : "font-bold text-amber-300"}>{pool.toLocaleString("uk-UA")}</span>
        {" / "}
        <span className="font-bold">{need.toLocaleString("uk-UA")}</span>
      </p>

      <GameButton onClick={press} disabled={busy || drinkJar.isPending} variant="accent">
        Підняти рівень
      </GameButton>

      {/* Що дасть рівень — до витрати досвіду */}
      {statGains(hero.stats, hero.nextLevelStats).length > 0 && (
        <p className="text-xs text-emerald-300">{statGains(hero.stats, hero.nextLevelStats).join(" · ")}</p>
      )}

      {!affordable && offering && (
        <div className="w-full space-y-2 rounded-xl bg-[#16305e]/80 p-3 text-left ring-1 ring-[#335c9a]">
          <p className="text-sm text-amber-200">Бракує {missing.toLocaleString("uk-UA")} досвіду.</p>

          {jars.length === 0 ? (
            <p className="text-sm text-sky-100/80">Банок досвіду в рюкзаку немає.</p>
          ) : (
            jars.map(({ item, experience }) => (
              <div key={item.itemKey} className="flex items-center gap-2">
                <ItemIcon itemKey={item.itemKey} type={item.type} rarity={item.rarity} size={32} />
                <span className="flex-1 text-sm">
                  {item.displayName} ×{item.count.toLocaleString("uk-UA")}
                  {experience > 0 && (
                    <span className="text-sky-200/80"> · +{(experience * item.count).toLocaleString("uk-UA")}</span>
                  )}
                </span>
                <GameButton
                  variant="secondary"
                  onClick={() =>
                    drinkJar.mutate({ itemKey: item.itemKey, count: Math.min(item.count, 10_000), targetX: null, targetY: null })
                  }
                  disabled={busy || drinkJar.isPending}
                >
                  Використати всі
                </GameButton>
              </div>
            ))
          )}

          <Link to={`/shop#${JAR_CHEST}`} className="inline-block text-sm text-sky-300 hover:underline">
            {catalog.itemName(JAR_CHEST)} у крамниці — банки досвіду всередині →
          </Link>
        </div>
      )}
    </div>
  );
}
