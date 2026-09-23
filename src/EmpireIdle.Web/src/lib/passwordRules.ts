/**
 * Дзеркалить вимоги Identity з беку: довжина 8, цифра, мала й велика літери.
 * Спецсимвол не потрібен.
 *
 * Дублювання свідоме: сервер віддає повідомлення Identity англійською,
 * і показувати їх гравцю не годиться. Якщо правила на беку зміняться,
 * правити треба обидва боки.
 */
export interface PasswordRule {
  label: string;
  passed: (password: string) => boolean;
}

export const passwordRules: PasswordRule[] = [
  { label: "Щонайменше 8 символів", passed: (value) => value.length >= 8 },
  { label: "Хоча б одна цифра", passed: (value) => /\d/.test(value) },
  { label: "Хоча б одна мала літера", passed: (value) => /[a-zа-яіїєґ]/.test(value) },
  { label: "Хоча б одна велика літера", passed: (value) => /[A-ZА-ЯІЇЄҐ]/.test(value) },
];

export function passwordIsValid(password: string): boolean {
  return passwordRules.every((rule) => rule.passed(password));
}
