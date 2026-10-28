/**
 * Дзеркалить вимоги Identity з беку: довжина 8, цифра, мала й велика літери.
 * Спецсимвол не потрібен.
 *
 * Дублювання свідоме: сервер віддає повідомлення Identity англійською,
 * і показувати їх гравцю не годиться. Якщо правила на беку зміняться,
 * правити треба обидва боки.
 */
export interface PasswordRule {
  /** Ключ підпису в `register.passwordRules`. */
  key: "length" | "digit" | "lower" | "upper";
  passed: (password: string) => boolean;
}

export const passwordRules: PasswordRule[] = [
  { key: "length", passed: (value) => value.length >= 8 },
  { key: "digit", passed: (value) => /\d/.test(value) },
  { key: "lower", passed: (value) => /[a-zа-яіїєґ]/.test(value) },
  { key: "upper", passed: (value) => /[A-ZА-ЯІЇЄҐ]/.test(value) },
];

export function passwordIsValid(password: string): boolean {
  return passwordRules.every((rule) => rule.passed(password));
}
