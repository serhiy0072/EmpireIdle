/**
 * Назви вбудованих ролей клану за ключем із сервера. Роль, яку клан створив
 * або перейменував, ключа не має — тоді показуємо назву, яку дав клан.
 */
const BUILT_IN_ROLES: Record<string, string> = {
  leader: "Лідер",
  deputy: "Заступник",
  general: "Генерал",
  officer: "Офіцер",
  veteran: "Ветеран",
  member: "Учасник",
};

export function clanRoleName(key: string | number | null | undefined, name: string | number | undefined): string {
  return (key == null || key === "" ? undefined : BUILT_IN_ROLES[String(key)]) ?? String(name ?? "");
}
