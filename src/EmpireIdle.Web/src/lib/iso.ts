/**
 * Ізометрія 2:1 для плану села.
 *
 * Координати в конфізі — відсотки від квадратної ділянки 0–100.
 * Трактуємо їх як площину землі й проєктуємо ромбом: так ділянка
 * виглядає як на мапах ігор жанру, а конфіг лишається простим.
 */
export const UNIT_X = 10;
export const UNIT_Y = 5;

/** Половина сторони будівлі в одиницях плану. Крок у конфізі 10, тож 4 лишає прохід між будівлями. */
export const FOOTPRINT = 4;

export interface Point {
  x: number;
  y: number;
}

export function project(x: number, y: number): Point {
  return { x: (x - y) * UNIT_X, y: (x + y) * UNIT_Y };
}

/** Точка плану на висоті z пікселів над землею. */
export function at(x: number, y: number, z = 0): Point {
  const point = project(x, y);
  return { x: point.x, y: point.y - z };
}

/** Три видимі грані: верх світліший, ліва середня, права в тіні. */
export interface Faces {
  top: string;
  left: string;
  right: string;
}

export const faces = (top: string, left: string, right: string): Faces => ({ top, left, right });

export function toPath(points: Point[]): string {
  return `${points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ")} Z`;
}

/** Межі всієї ділянки після проєкції плюс запас зверху під висоту будівель. */
export const WORLD = {
  minX: -100 * UNIT_X,
  maxX: 100 * UNIT_X,
  minY: -120,
  maxY: 200 * UNIT_Y,
} as const;
