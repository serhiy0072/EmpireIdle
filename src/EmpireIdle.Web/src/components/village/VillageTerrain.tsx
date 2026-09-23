import type { ReactElement } from "react";
import { at, faces, project, toPath, type Point } from "../../lib/iso";
import { Fence, Tree } from "./isoDetails";
import { Box, Pyramid } from "./isoShapes";

/**
 * Околиці села: земля за муром, озеро з річкою, пагорби, ліс, поля й дорога
 * від брами. Усе детерміноване — насіння фіксоване, тож дерева не стрибають
 * між рендерами. Нічого з цього не інтерактивне.
 */

/** Межі землі в одиницях плану — мур стоїть на 5..95. */
export const LAND_FROM = -45;
export const LAND_TO = 145;

/** Далі за це x або y елемент стоїть перед муром і малюється після будівель. */
const WALL_NEAR = 95;

const GRASS = "#c5e1a5";
const GRASS_DARK = "#aacf85";
const SAND = "#e8dcb5";
const WATER_DEEP = "#38bdf8";
const WATER_SHALLOW = "#7dd3fc";
const ROAD = "#d9c9a3";
const HILL = faces("#b5c99a", "#8fae74", "#6e8d57");
const ROCK = faces("#cbd5e1", "#94a3b8", "#64748b");
const SOIL = faces("#b0783a", "#8f5f2b", "#6e4720");

/** mulberry32 — маленький детермінований генератор, щоб ландшафт не змінювався між рендерами. */
function rng(seed: number): () => number {
  let a = seed >>> 0;

  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** Замкнена крива з нерівним радіусом навколо центру — берег озера, узлісся. */
function blob(cx: number, cy: number, radius: number, wobble: number, seed: number, steps = 18): Point[] {
  const random = rng(seed);
  const points: Point[] = [];

  for (let i = 0; i < steps; i++) {
    const angle = (i / steps) * Math.PI * 2;
    const r = radius * (1 - wobble / 2 + random() * wobble);
    points.push({ x: cx + Math.cos(angle) * r, y: cy + Math.sin(angle) * r * 0.85 });
  }

  return points;
}

/** Плавна замкнена SVG-крива через проєктовані точки (квадратичні сегменти між серединами). */
function smooth(points: Point[]): string {
  const projected = points.map((p) => project(p.x, p.y));
  const mid = (a: Point, b: Point) => ({ x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 });
  let d = "";

  for (let i = 0; i < projected.length; i++) {
    const current = projected[i] as Point;
    const next = projected[(i + 1) % projected.length] as Point;
    const m = mid(current, next);

    d += i === 0 ? `M${m.x.toFixed(1)} ${m.y.toFixed(1)}` : "";
    const after = projected[(i + 2) % projected.length] as Point;
    const m2 = mid(next, after);
    d += ` Q${next.x.toFixed(1)} ${next.y.toFixed(1)} ${m2.x.toFixed(1)} ${m2.y.toFixed(1)}`;
  }

  return `${d} Z`;
}

/** Відкрита плавна крива (річка, дорога): стрічка задається штрихом. */
function ribbon(points: Point[]): string {
  const projected = points.map((p) => project(p.x, p.y));
  const first = projected[0] as Point;
  let d = `M${first.x.toFixed(1)} ${first.y.toFixed(1)}`;

  for (let i = 1; i < projected.length - 1; i++) {
    const current = projected[i] as Point;
    const next = projected[i + 1] as Point;
    const m = { x: (current.x + next.x) / 2, y: (current.y + next.y) / 2 };
    d += ` Q${current.x.toFixed(1)} ${current.y.toFixed(1)} ${m.x.toFixed(1)} ${m.y.toFixed(1)}`;
  }

  const last = projected[projected.length - 1] as Point;
  return `${d} L${last.x.toFixed(1)} ${last.y.toFixed(1)}`;
}

interface Placed {
  x: number;
  y: number;
  node: ReactElement;
}

function isFront(p: { x: number; y: number }): boolean {
  return p.x > WALL_NEAR || p.y > WALL_NEAR;
}

/** Озеро на захід від муру, річка з нього тече на південь повз поля. */
const LAKE = blob(-24, 62, 22, 0.5, 7);
const RIVER = [
  { x: -12, y: 78 },
  { x: -2, y: 96 },
  { x: 4, y: 112 },
  { x: 2, y: 130 },
  { x: 10, y: 145 },
];
const ROAD_PATH = [
  { x: 50, y: 96 },
  { x: 52, y: 108 },
  { x: 60, y: 120 },
  { x: 74, y: 132 },
  { x: 84, y: 145 },
];

/** Три поля на південному заході, між річкою й дорогою. */
const FIELDS = [
  { x: 24, y: 112, hx: 7, hy: 5 },
  { x: 24, y: 128, hx: 7, hy: 5 },
  { x: 42, y: 136, hx: 6, hy: 4 },
];

/** Відстань від точки до ламаної — для річки й дороги. */
function distanceToPolyline(p: Point, line: Point[]): number {
  let best = Number.POSITIVE_INFINITY;

  for (let i = 0; i < line.length - 1; i++) {
    const a = line[i] as Point;
    const b = line[i + 1] as Point;
    const dx = b.x - a.x;
    const dy = b.y - a.y;
    const t = Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / (dx * dx + dy * dy)));
    best = Math.min(best, Math.hypot(p.x - (a.x + dx * t), p.y - (a.y + dy * t)));
  }

  return best;
}

/**
 * Чи можна поставити щось із радіусом r у точку p: не в мурі й не впритул до нього,
 * не в озері, не на річці, не на дорозі й не на полі. Один предикат для пагорбів
 * і дерев — інакше кожен вид декору вигадував би власні винятки.
 */
function blocked(p: Point, r: number): boolean {
  const wallMargin = 4 + r;
  if (p.x > -wallMargin && p.x < 100 + wallMargin && p.y > -wallMargin && p.y < 100 + wallMargin) return true;
  if (Math.hypot(p.x + 24, (p.y - 62) / 0.85) < 26 + r) return true;
  if (distanceToPolyline(p, RIVER) < 7 + r) return true;
  if (distanceToPolyline(p, ROAD_PATH) < 6 + r) return true;

  return FIELDS.some((f) => Math.abs(p.x - f.x) < f.hx + 2 + r && Math.abs(p.y - f.y) < f.hy + 2 + r);
}

/**
 * Пагорби на півночі й північному сході, кілька — за озером. Кандидат
 * відкидається, якщо стоїть на воді, дорозі, полі чи налазить на сусіда.
 */
const HILLS: { x: number; y: number; r: number; h: number }[] = (() => {
  const random = rng(11);
  const zones = [
    { x: 104, w: 38, y: -40, h: 64, count: 7 },
    { x: -8, w: 74, y: -44, h: 26, count: 6 },
    { x: -45, w: 20, y: 2, h: 30, count: 3 },
  ];
  const placed: { x: number; y: number; r: number; h: number }[] = [];

  for (const zone of zones) {
    let accepted = 0;

    for (let attempt = 0; attempt < zone.count * 12 && accepted < zone.count; attempt++) {
      const r = 5 + random() * 7;
      const candidate = { x: zone.x + random() * zone.w, y: zone.y + random() * zone.h, r, h: 24 + random() * 46 };

      if (blocked(candidate, r)) continue;
      if (placed.some((other) => Math.hypot(other.x - candidate.x, other.y - candidate.y) < other.r + r + 1)) continue;

      placed.push(candidate);
      accepted++;
    }
  }

  return placed;
})();

/** Ліс: узлісся на сході й південному сході, гай на північному заході, поодинокі дерева вздовж дороги. */
const TREES: { x: number; y: number; s: number; kind: "pine" | "round" }[] = (() => {
  const random = rng(23);
  const placed: { x: number; y: number; s: number; kind: "pine" | "round" }[] = [];

  const cluster = (cx: number, cy: number, rx: number, ry: number, count: number, pine: number) => {
    let accepted = 0;

    for (let attempt = 0; attempt < count * 10 && accepted < count; attempt++) {
      const candidate = {
        x: cx + (random() * 2 - 1) * rx,
        y: cy + (random() * 2 - 1) * ry,
        s: 1 + random() * 0.8,
        kind: (random() < pine ? "pine" : "round") as "pine" | "round",
      };

      if (candidate.x < LAND_FROM + 2 || candidate.x > LAND_TO - 2 || candidate.y < LAND_FROM + 2 || candidate.y > LAND_TO - 2) continue;
      if (blocked(candidate, 1.5)) continue;
      // Дерево на схилі пагорба висіло б у повітрі
      if (HILLS.some((hill) => Math.hypot(hill.x - candidate.x, hill.y - candidate.y) < hill.r + 1.5)) continue;
      if (placed.some((other) => Math.hypot(other.x - candidate.x, other.y - candidate.y) < 2.4)) continue;

      placed.push(candidate);
      accepted++;
    }
  };

  cluster(122, 84, 18, 26, 34, 0.6);
  cluster(116, 124, 24, 14, 18, 0.4);
  cluster(-22, -18, 20, 18, 24, 0.7);
  cluster(32, -28, 24, 8, 14, 0.8);
  cluster(-32, 108, 10, 18, 10, 0.3);
  cluster(44, 120, 8, 5, 4, 0.2);
  cluster(100, 130, 12, 8, 6, 0.5);

  return placed;
})();

function Field({ x, y, hx, hy }: { x: number; y: number; hx: number; hy: number }): ReactElement {
  const rows: number[] = [];
  for (let row = -hy + 1.2; row < hy; row += 1.6) rows.push(row);

  return (
    <g>
      <Box cx={x} cy={y} hx={hx} hy={hy} h={1} faces={SOIL} />
      {rows.map((row) => {
        const from = at(x - hx + 0.6, y + row, 1);
        const to = at(x + hx - 0.6, y + row, 1);
        return <line key={row} x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke="#4d7c0f" strokeWidth={2.4} strokeLinecap="round" />;
      })}
      <Fence from={{ x: x - hx, y: y + hy }} to={{ x: x + hx, y: y + hy }} h={3} />
      <Fence from={{ x: x + hx, y: y + hy }} to={{ x: x + hx, y: y - hy }} h={3} />
    </g>
  );
}

const DECOR: Placed[] = [
  ...HILLS.map((hill) => ({
    x: hill.x,
    y: hill.y,
    node: <Pyramid cx={hill.x} cy={hill.y} hx={hill.r} hy={hill.r} z={0} h={hill.h} faces={hill.h > 55 ? ROCK : HILL} />,
  })),
  ...TREES.map((tree) => ({ x: tree.x, y: tree.y, node: <Tree x={tree.x} y={tree.y} s={tree.s} kind={tree.kind} /> })),
  ...FIELDS.map((field) => ({ x: field.x, y: field.y, node: <Field {...field} /> })),
];

/** Дальні елементи — до будівель, ближні — після; всередині групи від дальніх до ближніх. */
const BACK = DECOR.filter((item) => !isFront(item)).sort((a, b) => a.x + a.y - (b.x + b.y));
const FRONT = DECOR.filter(isFront).sort((a, b) => a.x + a.y - (b.x + b.y));

interface Props {
  part: "ground" | "front";
}

export default function VillageTerrain({ part }: Props): ReactElement {
  if (part === "front") {
    return (
      <g pointerEvents="none">
        {FRONT.map((item, index) => (
          <g key={index}>{item.node}</g>
        ))}
      </g>
    );
  }

  const land = [
    project(LAND_FROM, LAND_FROM),
    project(LAND_TO, LAND_FROM),
    project(LAND_TO, LAND_TO),
    project(LAND_FROM, LAND_TO),
  ];
  // Піщана кромка навколо суходолу: край землі, а не обрізаний полігон
  const beach = [
    project(LAND_FROM - 3, LAND_FROM - 3),
    project(LAND_TO + 3, LAND_FROM - 3),
    project(LAND_TO + 3, LAND_TO + 3),
    project(LAND_FROM - 3, LAND_TO + 3),
  ];
  const shore = blob(-24, 62, 25, 0.5, 7);

  return (
    <g pointerEvents="none">
      <path d={toPath(beach)} fill={SAND} />
      <path d={toPath(land)} fill={GRASS} />
      {/* Легкі плями темнішої трави, щоб земля не була однією площиною */}
      <path d={smooth(blob(112, 40, 26, 0.6, 3))} fill={GRASS_DARK} opacity={0.5} />
      <path d={smooth(blob(20, -10, 30, 0.6, 5))} fill={GRASS_DARK} opacity={0.4} />
      <path d={smooth(blob(60, 122, 24, 0.7, 9))} fill={GRASS_DARK} opacity={0.4} />

      {/* Річка — під озером, щоб берег озера накрив її початок */}
      <path d={ribbon(RIVER)} fill="none" stroke={SAND} strokeWidth={34} strokeLinecap="round" />
      <path d={ribbon(RIVER)} fill="none" stroke={WATER_SHALLOW} strokeWidth={24} strokeLinecap="round" />
      <path d={ribbon(RIVER)} fill="none" stroke={WATER_DEEP} strokeWidth={10} strokeLinecap="round" opacity={0.6} />

      <path d={smooth(shore)} fill={SAND} />
      <path d={smooth(LAKE)} fill={WATER_SHALLOW} />
      <path d={smooth(blob(-24, 62, 14, 0.4, 7))} fill={WATER_DEEP} opacity={0.7} />
      {[0, 1, 2].map((i) => {
        const p = project(-30 + i * 7, 56 + i * 6);
        return <ellipse key={i} cx={p.x} cy={p.y} rx={16 + i * 4} ry={5 + i} fill="none" stroke="#e0f2fe" strokeWidth={1.2} opacity={0.8} />;
      })}

      {/* Дорога від брами на південний схід */}
      <path d={ribbon(ROAD_PATH)} fill="none" stroke={ROAD} strokeWidth={30} strokeLinecap="round" />
      <path d={ribbon(ROAD_PATH)} fill="none" stroke="#c9b68d" strokeWidth={4} strokeDasharray="10 14" strokeLinecap="round" opacity={0.7} />

      {BACK.map((item, index) => (
        <g key={index}>{item.node}</g>
      ))}
    </g>
  );
}
