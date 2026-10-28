import { t } from "i18next";
/**
 * Назви вбудованих ролей клану за ключем із сервера. Роль, яку клан створив
 * або перейменував, ключа не має — тоді показуємо назву, яку дав клан.
 */
const BUILT_IN_ROLES = ["leader", "deputy", "general", "officer", "veteran", "member"] as const;

function isBuiltInRole(key: string): key is (typeof BUILT_IN_ROLES)[number] {
  return (BUILT_IN_ROLES as readonly string[]).includes(key);
}

export function clanRoleName(key: string | number | null | undefined, name: string | number | undefined): string {
  const builtIn = key == null ? "" : String(key);

  return isBuiltInRole(builtIn) ? t(`social:clanRole.${builtIn}`) : String(name ?? "");
}
