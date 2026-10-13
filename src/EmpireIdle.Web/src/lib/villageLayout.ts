import { UNIT_X, UNIT_Y } from "./iso";

/**
 * Правила плану села — дзеркало VillageLayout на сервері (рішення 13.10.2026).
 * Веб перевіряє їх, щоб підсвітити недопустиме місце ще до запиту; останнє слово за сервером.
 */
export const GRID_STEP = 2;
export const PLAN_MIN = 12;
export const PLAN_MAX = 88;
export const MIN_SPACING = 12;

export interface PlanPoint {
  x: number;
  y: number;
}

/** Точка SVG-полотна (до проєкції) назад у координати плану. */
export function unproject(sx: number, sy: number): PlanPoint {
  const diff = sx / UNIT_X;
  const sum = sy / UNIT_Y;
  return { x: (sum + diff) / 2, y: (sum - diff) / 2 };
}

/** Найближчий вузол сітки, притиснутий до меж муру. */
export function snap(point: PlanPoint): PlanPoint {
  const axis = (value: number) => Math.min(PLAN_MAX, Math.max(PLAN_MIN, Math.round(value / GRID_STEP) * GRID_STEP));
  return { x: axis(point.x), y: axis(point.y) };
}

/** Чи налазять будівлі з такими центрами одна на одну. */
export function overlaps(a: PlanPoint, b: PlanPoint): boolean {
  return Math.max(Math.abs(a.x - b.x), Math.abs(a.y - b.y)) < MIN_SPACING;
}
