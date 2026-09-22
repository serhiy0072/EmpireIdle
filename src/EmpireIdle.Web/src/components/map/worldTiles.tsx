import type { ReactElement } from "react";
import { at, faces, project, toPath, type Faces } from "../../lib/iso";
import { Tree } from "../village/isoDetails";
import { Box, Cone, Cylinder, Flag, Pyramid } from "../village/isoShapes";

/**
 * Тайли світової мапи з тих самих примітивів, що й село: одна клітина —
 * ромб зі стороною TILE одиниць плану, місцевість зверху. Все детерміноване
 * від координат клітини, тож сусідні ділянки стикуються без швів.
 */
export const TILE = 3;

/** Центр клітини (x, y) у координатах плану. */
export function cellOrigin(x: number, y: number): { x: number; y: number } {
  return { x: x * TILE, y: y * TILE };
}

const GROUND: Record<string, string> = {
  plain: "#c5e1a5",
  forest: "#a3c98a",
  mountain: "#b8b2a6",
  water: "#7dd3fc",
  peaks: "#d6d3d1",
  swamp: "#9cb572",
};

const PEAK = faces("#f1f5f9", "#cbd5e1", "#94a3b8");
const ROCK = faces("#c8c2b6", "#9e978a", "#6f6960");
const REED = faces("#4d7c0f", "#3f6212", "#365314");

/** Хеш від координат: одна й та сама клітина завжди отримує ті самі «випадкові» деталі. */
function hash(x: number, y: number, salt: number): number {
  let h = (x * 374761393 + y * 668265263 + salt * 2246822519) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967296;
}

export function groundFill(type: string): string {
  return GROUND[type] ?? "#e2e8f0";
}

/** Ромб основи клітини. */
export function tilePath(x: number, y: number): string {
  const { x: cx, y: cy } = cellOrigin(x, y);
  const h = TILE / 2;

  return toPath([at(cx - h, cy - h), at(cx + h, cy - h), at(cx + h, cy + h), at(cx - h, cy + h)]);
}

/**
 * Деталі поверх основи. Порожній фрагмент для рівнини — більшість клітин
 * рівнина, і саме вони тримають кількість вузлів SVG у нормі.
 */
export function tileDetail(type: string, x: number, y: number): ReactElement | null {
  const { x: cx, y: cy } = cellOrigin(x, y);
  const r1 = hash(x, y, 1);
  const r2 = hash(x, y, 2);
  const r3 = hash(x, y, 3);

  switch (type) {
    case "forest": {
      const count = 1 + Math.floor(r1 * 2);
      return (
        <g>
          {Array.from({ length: count }, (_, i) => {
            const ox = (hash(x, y, 10 + i) - 0.5) * 1.6;
            const oy = (hash(x, y, 20 + i) - 0.5) * 1.6;
            return <Tree key={i} x={cx + ox} y={cy + oy} s={0.35 + hash(x, y, 30 + i) * 0.2} kind={r2 < 0.5 ? "pine" : "round"} />;
          })}
        </g>
      );
    }
    case "mountain":
      return <Pyramid cx={cx + (r1 - 0.5) * 0.6} cy={cy + (r2 - 0.5) * 0.6} hx={1.1 + r3 * 0.3} hy={1.1 + r3 * 0.3} z={0} h={10 + r1 * 8} faces={ROCK} />;
    case "peaks":
      return (
        <g>
          <Pyramid cx={cx} cy={cy} hx={1.4} hy={1.4} z={0} h={20 + r1 * 10} faces={ROCK} />
          <Pyramid cx={cx} cy={cy} hx={0.6} hy={0.6} z={12 + r1 * 6} h={9 + r1 * 4} faces={PEAK} />
        </g>
      );
    case "water": {
      const p = project(cx, cy);
      return r1 < 0.45 ? (
        <ellipse cx={p.x + (r2 - 0.5) * 10} cy={p.y + (r3 - 0.5) * 4} rx={5 + r2 * 4} ry={1.6} fill="none" stroke="#e0f2fe" strokeWidth={0.8} opacity={0.8} />
      ) : null;
    }
    case "swamp": {
      const p = project(cx + (r1 - 0.5), cy + (r2 - 0.5));
      return (
        <g>
          <ellipse cx={p.x} cy={p.y} rx={6} ry={2.5} fill="#5b7a3a" opacity={0.5} />
          <Cylinder cx={cx + 0.6} cy={cy - 0.4} r={0.08} h={4 + r3 * 3} faces={REED} />
          <Cylinder cx={cx - 0.5} cy={cy + 0.5} r={0.08} h={3 + r1 * 3} faces={REED} />
        </g>
      );
    }
    default:
      // Рівнина: зрідка камінець або кущ, щоб поле не було порожнім
      if (r1 < 0.06) return <Box cx={cx + (r2 - 0.5)} cy={cy + (r3 - 0.5)} hx={0.25} hy={0.2} h={1.2} faces={ROCK} />;
      if (r1 < 0.14) return <Tree x={cx + (r2 - 0.5) * 1.4} y={cy + (r3 - 0.5) * 1.4} s={0.22} />;
      return null;
  }
}

const HOME_ROOF = faces("#f87171", "#dc2626", "#991b1b");
const HOME_WALL = faces("#e2e8f0", "#94a3b8", "#64748b");
const OTHER_ROOF = faces("#93c5fd", "#3b82f6", "#1d4ed8");
const OTHER_WALL = faces("#e7c9a0", "#b88a5a", "#8a6238");
const MONSTER = faces("#fca5a5", "#ef4444", "#991b1b");
const BONE = faces("#f8fafc", "#e2e8f0", "#cbd5e1");

/** Мітка окупанта клітини: своє село — червоний дах і прапор, чуже — синій, монстр — намет із черепом. */
export function occupantArt(kind: string, x: number, y: number, isHome: boolean): ReactElement {
  const { x: cx, y: cy } = cellOrigin(x, y);

  if (kind === "Monster") {
    const skull = at(cx, cy, 16);
    return (
      <g>
        <Cone cx={cx} cy={cy} r={1.3} z={0} h={11} faces={MONSTER} />
        <circle cx={skull.x} cy={skull.y} r={3.2} fill={BONE.top} stroke="#1e293b" strokeWidth={0.6} />
        <circle cx={skull.x - 1.1} cy={skull.y - 0.4} r={0.7} fill="#1e293b" />
        <circle cx={skull.x + 1.1} cy={skull.y - 0.4} r={0.7} fill="#1e293b" />
      </g>
    );
  }

  const roof: Faces = isHome ? HOME_ROOF : OTHER_ROOF;
  const wall: Faces = isHome ? HOME_WALL : OTHER_WALL;
  const h = isHome ? 9 : 7;

  return (
    <g>
      <Box cx={cx} cy={cy} hx={1.1} hy={1.1} h={h} faces={wall} />
      <Pyramid cx={cx} cy={cy} hx={1.3} hy={1.3} z={h} h={7} faces={roof} />
      {isHome && <Flag x={cx} y={cy} z={h + 7} h={10} color="#f59e0b" />}
    </g>
  );
}
