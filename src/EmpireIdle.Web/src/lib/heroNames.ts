/**
 * Назва героя з ключа. Тимчасовий шар: бек віддає лише heroKey,
 * а назви, ранг і клас лишаються в конфізі. Замінити на каталог героїв,
 * щойно він з'явиться в API — дублювати конфіг у клієнті не будемо.
 */
export function heroName(heroKey: string): string {
  return heroKey
    .split("_")
    .map((part) => (part.length === 0 ? part : part[0]!.toUpperCase() + part.slice(1)))
    .join(" ");
}

const STATES: Record<string, string> = {
  Idle: "Вдома",
  Deployed: "У поході",
  Wounded: "Поранений",
  LevelingUp: "Качається",
};

export function heroState(state: string): string {
  return STATES[state] ?? state;
}
