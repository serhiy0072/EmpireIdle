/**
 * Дзеркалить CreateClanCommandValidator на беку: назва 3–32 символи з літер,
 * цифр, пробілів, апострофів і дефісів; тег 2–5 латинських літер або цифр.
 *
 * Без дзеркала закоротку назву відхиляла серверна валідація, і гравець бачив
 * лише «Щось пішло не так». Якщо правило на беку зміниться, правити обидва боки.
 */
export interface ClanRule {
  label: string;
  passed: (value: string) => boolean;
}

export const clanNameRules: ClanRule[] = [
  { label: "Назва від 3 до 32 символів", passed: (value) => value.length >= 3 && value.length <= 32 },
  {
    label: "Літери, цифри, пробіл, апостроф або дефіс",
    passed: (value) => value.length > 0 && /^[\p{L}0-9 '-]+$/u.test(value),
  },
];

export const clanTagRules: ClanRule[] = [
  { label: "Тег від 2 до 5 символів", passed: (value) => value.length >= 2 && value.length <= 5 },
  { label: "Тег лише з латинських літер і цифр", passed: (value) => value.length > 0 && /^[A-Za-z0-9]+$/.test(value) },
];

export function clanNameIsValid(name: string): boolean {
  return clanNameRules.every((rule) => rule.passed(name));
}

export function clanTagIsValid(tag: string): boolean {
  return clanTagRules.every((rule) => rule.passed(tag));
}
