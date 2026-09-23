/**
 * Дзеркалить правило імені з RegisterRequest на беку: 3–50 символів,
 * латинські літери, цифри, крапка, дефіс і підкреслення.
 *
 * Без дзеркала сервер відхиляв ім'я автоматичною валідацією моделі без
 * errorCode, і гравець бачив лише «Щось пішло не так». Якщо правило на беку
 * зміниться, правити треба обидва боки.
 */
export interface UserNameRule {
  label: string;
  passed: (userName: string) => boolean;
}

export const userNameRules: UserNameRule[] = [
  { label: "Від 3 до 50 символів", passed: (value) => value.length >= 3 && value.length <= 50 },
  {
    label: "Латинські літери, цифри, крапка, дефіс або підкреслення",
    passed: (value) => value.length > 0 && /^[A-Za-z0-9._-]+$/.test(value),
  },
];

export function userNameIsValid(userName: string): boolean {
  return userNameRules.every((rule) => rule.passed(userName));
}
