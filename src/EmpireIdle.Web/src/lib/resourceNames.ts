import { t } from "i18next";
/**
 * Назви ресурсів і валют у родовому відмінку — для відмов на кшталт
 * «Не вистачає золота». Каталог дає лише
 * називний відмінок, тому тут власні ключі; англійською це просто назва з малої літери.
 */
const GENITIVE = ["gold", "wood", "stone", "iron", "food", "fodder", "gems", "dungeon-energy", "heroExperience"] as const;

function hasGenitive(key: string): key is (typeof GENITIVE)[number] {
  return (GENITIVE as readonly string[]).includes(key);
}

/** Невідомий ключ повертається як є: краще сирий ключ, ніж порожнє місце в тексті. */
export function resourceGenitive(key: string): string {
  return hasGenitive(key) ? t(`common:resourceGenitive.${key}`) : key;
}
