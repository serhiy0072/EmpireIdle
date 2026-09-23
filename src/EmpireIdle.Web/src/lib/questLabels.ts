import type { QuestObjectiveView, RewardConfig } from "./apiTypes";
import type { Catalog } from "./queries/catalog";

/**
 * Текст цілі з її типу й цілі. Тип — ім'я доменної події на беку
 * (BuildingCollected, UnitsTrained…): новий тип квесту без підпису тут
 * покажеться технічним рядком, а не зламає екран.
 */
export function objectiveLabel(objective: QuestObjectiveView, catalog: Catalog): string {
  const target = objective.target ?? "";

  switch (objective.type) {
    case "BuildingCollected":
      return `Зібрати ${catalog.resourceName(target)}`;
    case "BuildingUpgradeCompleted":
      return `${catalog.buildingName(target)} до рівня ${objective.required}`;
    case "UnitsTrained":
      return target === "" ? "Навчити юнітів" : `Навчити: ${catalog.unitName(target)}`;
    case "MonsterDefeated":
      return "Перемогти монстрів";
    case "GemsSpent":
      return "Витратити самоцвітів";
    default:
      return target === "" ? objective.type : `${objective.type}: ${target}`;
  }
}

/** Порогова ціль (рівень будівлі) — це "досягти N", а не "накопичити N": прогрес-бар їй не пасує. */
export function objectiveIsThreshold(objective: QuestObjectiveView): boolean {
  return objective.type === "BuildingUpgradeCompleted";
}

/** Підпис нагороди: тип каже, у якому довіднику шукати назву. */
export function rewardLabel(reward: RewardConfig, catalog: Catalog): string {
  const amount = reward.amount.toLocaleString("uk-UA");
  const key = reward.key ?? "";

  switch (reward.type) {
    case "Gems":
      return `${amount} 💎`;
    case "Resource":
      return `${amount} ${catalog.resourceName(key)}`;
    case "Hero":
      return `Герой: ${catalog.heroName(key)}`;
    case "Equipment":
    case "Item":
      return reward.amount > 1 ? `${catalog.itemName(key)} ×${amount}` : catalog.itemName(key);
    default:
      return `${reward.type} ${key} ×${amount}`;
  }
}
