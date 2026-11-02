/**
 * Дзеркалить CreateClanCommandValidator на беку: назва 3–32 символи з літер,
 * цифр, пробілів, апострофів і дефісів; тег 2–5 латинських літер або цифр.
 *
 * Без дзеркала закоротку назву відхиляла серверна валідація, і гравець бачив
 * лише «Щось пішло не так». Якщо правило на беку зміниться, правити обидва боки.
 */
export interface ClanRule {
  /** Ключ підпису в `social:clanRules`. */
  key: "nameLength" | "nameCharset" | "tagLength" | "tagCharset";
  passed: (value: string) => boolean;
}

export const clanNameRules: ClanRule[] = [
  { key: "nameLength", passed: (value) => value.length >= 3 && value.length <= 32 },
  {
    key: "nameCharset",
    passed: (value) => value.length > 0 && /^[\p{L}0-9 '-]+$/u.test(value),
  },
];

export const clanTagRules: ClanRule[] = [
  { key: "tagLength", passed: (value) => value.length >= 2 && value.length <= 5 },
  { key: "tagCharset", passed: (value) => value.length > 0 && /^[A-Za-z0-9]+$/.test(value) },
];

export function clanNameIsValid(name: string): boolean {
  return clanNameRules.every((rule) => rule.passed(name));
}

export function clanTagIsValid(tag: string): boolean {
  return clanTagRules.every((rule) => rule.passed(tag));
}
