/**
 * Налаштування показу бою, що переживають бій: швидкість і авто-вміння. Сховище може бути недоступне
 * (приватне вікно) — тоді кожен бій починається з типових значень.
 */
const SPEED_KEY = "battle-replay-speed";
const AUTO_KEY = "battle-auto-skills";

function load(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function save(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Без сховища налаштування просто не запам'ятається
  }
}

export function loadSpeed(): number {
  return load(SPEED_KEY) === "2" ? 2 : 1;
}

export function saveSpeed(speed: number): void {
  save(SPEED_KEY, String(speed));
}

/** Авто-вміння ввімкнені, поки гравець сам їх не вимкнув. */
export function loadAutoSkills(): boolean {
  return load(AUTO_KEY) !== "0";
}

export function saveAutoSkills(auto: boolean): void {
  save(AUTO_KEY, auto ? "1" : "0");
}
