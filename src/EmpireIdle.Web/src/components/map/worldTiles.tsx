import type { ReactElement } from "react";
import type { MapOccupantCell } from "../../lib/apiTypes";
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

/** Квадрат клітин на відстані Чебишова ≤ radius від (x, y) — зона дії кланової споруди. */
export function areaPath(x: number, y: number, radius: number): string {
  const { x: cx, y: cy } = cellOrigin(x, y);
  const h = TILE / 2 + radius * TILE;

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
const TOWER_STONE = faces("#e5e7eb", "#9ca3af", "#6b7280");
const SCAFFOLD = faces("#fde68a", "#d97706", "#92400e");
const OWN_CAP = faces("#6ee7b7", "#10b981", "#047857");
const FOREIGN_CAP = faces("#c4b5fd", "#8b5cf6", "#6d28d9");
const HIDE = faces("#7c3aed", "#5b21b6", "#3b0764");
const HORN = faces("#fef3c7", "#e7c9a0", "#b88a5a");
const CLAW = faces("#1e293b", "#0f172a", "#020617");
const FUR = { light: "#cbd5e1", body: "#94a3b8", dark: "#475569" };
const ORC = { light: "#86efac", body: "#4ade80", dark: "#15803d" };

/** Витоптана земля під істотою: спільна для всіх типів. */
function scorched(cx: number, cy: number, size: number): ReactElement {
  const p = project(cx, cy);
  return <ellipse cx={p.x} cy={p.y} rx={14 * size} ry={7 * size} fill="#3f2a14" opacity={0.45} />;
}

/** Вовк: присадкуватий сірий звір на чотирьох лапах, вуха й хвіст. */
function wolfArt(cx: number, cy: number, size: number): ReactElement {
  const body = at(cx, cy, 4 * size);
  const head = at(cx + 0.55, cy + 0.55, 7 * size);
  const tail = at(cx - 0.7, cy - 0.7, 6 * size);

  return (
    <g>
      {scorched(cx, cy, size)}
      <Box cx={cx - 0.5} cy={cy + 0.4} hx={0.14} hy={0.14} h={3 * size} faces={CLAW} />
      <Box cx={cx + 0.4} cy={cy - 0.5} hx={0.14} hy={0.14} h={3 * size} faces={CLAW} />
      <Box cx={cx + 0.3} cy={cy + 0.5} hx={0.14} hy={0.14} h={3 * size} faces={CLAW} />
      <Box cx={cx - 0.4} cy={cy - 0.4} hx={0.14} hy={0.14} h={3 * size} faces={CLAW} />
      <path d={`M${tail.x} ${tail.y} q-6 -6 -3 -12`} fill="none" stroke={FUR.dark} strokeWidth={2.4 * size} strokeLinecap="round" />
      <ellipse cx={body.x} cy={body.y} rx={10 * size} ry={5.5 * size} fill={FUR.body} stroke={FUR.dark} strokeWidth={0.6} />
      <ellipse cx={body.x - 2 * size} cy={body.y - 2 * size} rx={6 * size} ry={2.5 * size} fill={FUR.light} opacity={0.7} />
      <ellipse cx={head.x} cy={head.y} rx={4.5 * size} ry={3.8 * size} fill={FUR.body} stroke={FUR.dark} strokeWidth={0.6} />
      <path d={`M${head.x - 3 * size} ${head.y - 2 * size} l-1.5 -4 l3 1.5 Z`} fill={FUR.dark} />
      <path d={`M${head.x + 1 * size} ${head.y - 3 * size} l1 -4 l1.8 2.8 Z`} fill={FUR.dark} />
      <ellipse cx={head.x + 3.5 * size} cy={head.y + 1 * size} rx={2.2 * size} ry={1.4 * size} fill={FUR.dark} />
      <circle cx={head.x - 0.5 * size} cy={head.y - 0.6 * size} r={0.9 * size} fill="#fbbf24" />
      <circle cx={head.x + 1.6 * size} cy={head.y - 0.8 * size} r={0.9 * size} fill="#fbbf24" />
    </g>
  );
}

/** Орк: зелений здоровань із кийком і іклами. */
function orcArt(cx: number, cy: number, size: number): ReactElement {
  const body = at(cx, cy, 7 * size);
  const head = at(cx + 0.2, cy + 0.2, 15 * size);
  const club = at(cx + 0.9, cy - 0.5, 2 * size);

  return (
    <g>
      {scorched(cx, cy, size)}
      <Box cx={cx - 0.35} cy={cy + 0.35} hx={0.22} hy={0.22} h={4 * size} faces={CLAW} />
      <Box cx={cx + 0.35} cy={cy - 0.35} hx={0.22} hy={0.22} h={4 * size} faces={CLAW} />
      <path d={`M${club.x} ${club.y} l6 -22`} stroke="#78350f" strokeWidth={2.2 * size} strokeLinecap="round" />
      <ellipse cx={club.x + 6 * size} cy={club.y - 22 * size} rx={3.2 * size} ry={4 * size} fill="#57534e" stroke="#292524" strokeWidth={0.6} />
      <ellipse cx={body.x} cy={body.y} rx={8 * size} ry={7 * size} fill={ORC.body} stroke={ORC.dark} strokeWidth={0.6} />
      <path d={`M${body.x - 8 * size} ${body.y} q8 -3 16 0 v4 q-8 3 -16 0 Z`} fill="#78350f" />
      <circle cx={head.x} cy={head.y} r={5 * size} fill={ORC.body} stroke={ORC.dark} strokeWidth={0.6} />
      <circle cx={head.x - 1.8 * size} cy={head.y - 0.6 * size} r={1 * size} fill="#fef2f2" />
      <circle cx={head.x + 1.8 * size} cy={head.y - 0.6 * size} r={1 * size} fill="#fef2f2" />
      <circle cx={head.x - 1.8 * size} cy={head.y - 0.6 * size} r={0.5 * size} fill="#0f172a" />
      <circle cx={head.x + 1.8 * size} cy={head.y - 0.6 * size} r={0.5 * size} fill="#0f172a" />
      <path d={`M${head.x - 2.4 * size} ${head.y + 2.4 * size} l1 -3 l1 3 Z`} fill="#f8fafc" />
      <path d={`M${head.x + 0.4 * size} ${head.y + 2.4 * size} l1 -3 l1 3 Z`} fill="#f8fafc" />
    </g>
  );
}

/** Невідомий тип: рогата туша — те, що було до появи типів. */
function beastArt(cx: number, cy: number, size: number): ReactElement {
  const body = at(cx, cy, 5 * size);
  const head = at(cx + 0.35, cy + 0.35, 10 * size);

  return (
    <g>
      {scorched(cx, cy, size)}
      <Box cx={cx - 0.7} cy={cy + 0.9} hx={0.28} hy={0.22} h={2.5 * size} faces={CLAW} />
      <Box cx={cx + 0.9} cy={cy - 0.7} hx={0.22} hy={0.28} h={2.5 * size} faces={CLAW} />
      <ellipse cx={body.x} cy={body.y} rx={11 * size} ry={8 * size} fill={HIDE.left} stroke="#1e1b4b" strokeWidth={0.6} />
      <ellipse cx={body.x - 3 * size} cy={body.y - 3 * size} rx={7 * size} ry={4.5 * size} fill={HIDE.top} opacity={0.8} />
      <circle cx={head.x} cy={head.y} r={5.5 * size} fill={HIDE.top} stroke="#1e1b4b" strokeWidth={0.6} />
      <Cone cx={cx + 0.1} cy={cy + 0.9} r={0.28} z={12 * size} h={7 * size} faces={HORN} />
      <Cone cx={cx + 0.9} cy={cy - 0.1} r={0.28} z={12 * size} h={7 * size} faces={HORN} />
      <circle cx={head.x - 2 * size} cy={head.y - 0.5} r={1.4 * size} fill="#ef4444" />
      <circle cx={head.x + 2 * size} cy={head.y - 0.5} r={1.4 * size} fill="#ef4444" />
    </g>
  );
}

/**
 * Монстр за типом із конфіга. Рівень задає розмір: вовк 1-го рівня — цуценя
 * поруч із вовком 5-го. Невідомий тип отримує загальну істоту, а не порожнє місце.
 */
function monsterArt(cx: number, cy: number, type: string | null | undefined, level: number | null | undefined): ReactElement {
  const size = 0.8 + Math.min(Math.max((level ?? 1) - 1, 0), 11) * 0.05;

  switch (type) {
    case "wolf":
      return wolfArt(cx, cy, size);
    case "orc":
      return orcArt(cx, cy, size);
    default:
      return beastArt(cx, cy, size);
  }
}

/**
 * Кланова споруда — вежа: своя зелена, чужа фіолетова. Поки будується — низький
 * дерев'яний каркас без прапора: бонусу вона ще не дає.
 */
function structureArt(cx: number, cy: number, own: boolean, building: boolean): ReactElement {
  if (building) {
    return (
      <g opacity={0.85}>
        <Box cx={cx} cy={cy} hx={0.9} hy={0.9} h={5} faces={SCAFFOLD} />
        <Cone cx={cx} cy={cy} r={0.9} z={5} h={3} faces={own ? OWN_CAP : FOREIGN_CAP} />
      </g>
    );
  }

  return (
    <g>
      <Cylinder cx={cx} cy={cy} r={0.9} h={11} faces={TOWER_STONE} />
      <Cone cx={cx} cy={cy} r={1.1} z={11} h={5} faces={own ? OWN_CAP : FOREIGN_CAP} />
      <Flag x={cx} y={cy} z={16} h={8} color={own ? "#10b981" : "#8b5cf6"} />
    </g>
  );
}

/** Чи споруда ще будується: до ReadyAt бонусу немає. */
export function isStructureBuilding(occupant: MapOccupantCell, now: number): boolean {
  return occupant.readyAt != null && Date.parse(occupant.readyAt) > now;
}

/**
 * Мітка окупанта клітини: своє село — червоний дах і прапор, чуже — синій, монстр — істота свого типу,
 * кланова споруда — вежа (ownClanId відрізняє свою від чужої).
 */
export function occupantArt(occupant: MapOccupantCell, isHome: boolean, ownClanId: string | null, now: number): ReactElement {
  const { x: cx, y: cy } = cellOrigin(occupant.x, occupant.y);

  if (occupant.occupantType === "Monster") return monsterArt(cx, cy, occupant.monsterType, occupant.monsterLevel);

  if (occupant.occupantType === "ClanStructure") {
    return structureArt(cx, cy, ownClanId !== null && occupant.clanId === ownClanId, isStructureBuilding(occupant, now));
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
