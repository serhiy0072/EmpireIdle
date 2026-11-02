import { t } from "i18next";
/** Назви класів героїв. Ключі — як у конфігу сервера. */
export const HERO_CLASSES = ["warrior", "knight", "archer"] as const;

function isHeroClass(value: string): value is (typeof HERO_CLASSES)[number] {
  return (HERO_CLASSES as readonly string[]).includes(value);
}

/** Невідомий клас повертається ключем: краще сирий рядок, ніж порожнє місце. */
export function classLabel(heroClass: string | null | undefined): string {
  if (heroClass == null) return "—";

  return isHeroClass(heroClass) ? t(`common:heroClass.${heroClass}`) : heroClass;
}
