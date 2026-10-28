import { t } from "i18next";
import uk from "../locales/uk/technologies.json";

type NodeKey = keyof typeof uk.node;

/**
 * Назва вузла технологій мовою гравця. Структура дерева живе в конфігу сервера, а назви — на клієнті:
 * у конфігу технологій немає назв, лише ключі. Невідомий ключ повертається як є, щоб не показати порожнє місце.
 */
export function technologyName(key: string): string {
  return key in uk.node ? t(`technologies:node.${key as NodeKey}`) : key;
}
