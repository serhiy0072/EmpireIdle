import type { QuestObjectiveView, RewardConfig } from "./apiTypes";
import type { Catalog } from "./queries/catalog";
import { rarityLabel } from "./rarity";
import { formatNumber } from "./format";
import { t } from "i18next";

/**
 * Текст цілі з її типу й цілі. Тип — ім'я доменної події на беку
 * (BuildingCollected, UnitsTrained…): новий тип квесту без підпису тут
 * покажеться технічним рядком, а не зламає екран.
 */
export function objectiveLabel(objective: QuestObjectiveView, catalog: Catalog): string {
  const target = objective.target ?? "";

  switch (objective.type) {
    case "BuildingCollected":
      return t("social:objective.collect", { resource: catalog.resourceName(target) });
    case "BuildingUpgradeCompleted":
      return t("social:objective.upgrade", { building: catalog.buildingName(target), level: objective.required });
    case "UnitsTrained":
      return target === "" ? t("social:objective.trainAny") : t("social:objective.train", { unit: catalog.unitName(target) });
    case "MonsterDefeated":
      return t("social:objective.monsters");
    case "GemsSpent":
      return t("social:objective.gems");
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
  const amount = formatNumber(reward.amount);
  const key = reward.key ?? "";

  switch (reward.type) {
    case "Gems":
      return `${amount} 💎`;
    case "Resource":
      return `${amount} ${catalog.resourceName(key)}`;
    case "Hero":
      return t("social:reward.hero", { hero: catalog.heroName(key) });
    case "Equipment":
    case "Item":
      return reward.amount > 1 ? `${catalog.itemName(key)} ×${amount}` : catalog.itemName(key);
    case "HeroExperience":
      return t("social:reward.heroExperience", { amount });
    case "Artifact":
      // Випадковий артефакт рідкості: «Рідкісний артефакт ×2»
      return reward.amount > 1
        ? t("social:reward.artifactMany", { rarity: rarityLabel(key), amount })
        : t("social:reward.artifact", { rarity: rarityLabel(key) });
    default:
      return `${reward.type} ${key} ×${amount}`;
  }
}
