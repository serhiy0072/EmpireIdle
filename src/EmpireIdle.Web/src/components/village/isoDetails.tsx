import type { ReactElement } from "react";
import { at, toPath, type Faces } from "../../lib/iso";
import { Box, Cone, Cylinder } from "./isoShapes";

/**
 * Дрібні деталі будівель: вікна, двері, черепиця, димарі, дерева, паркани,
 * полотнища, ліхтарі. Кожна — з тих самих примітивів, що й корпуси, тож
 * стиль один на всю мапу.
 */

type Side = "left" | "right";

/**
 * Точка на видимій грані коробки: t — частка вздовж грані (0..1), z — висота.
 * Ліва грань іде вздовж X при y = cy + hy, права — вздовж Y при x = cx + hx.
 * Крихітний зсув назовні, щоб деталь не мерехтіла з площиною грані.
 */
function onFace(side: Side, cx: number, cy: number, hx: number, hy: number, t: number, z: number) {
  return side === "left"
    ? at(cx - hx + 2 * hx * t, cy + hy + 0.02, z)
    : at(cx + hx + 0.02, cy + hy - 2 * hy * t, z);
}

interface WindowsProps {
  cx: number;
  cy: number;
  hx: number;
  hy: number;
  /** Низ коробки, на якій вікна. */
  z?: number;
  /** Висота коробки — вікна розкладаються між z і z + h. */
  h: number;
  side: Side;
  rows?: number;
  cols?: number;
  glass?: string;
}

/** Ряди вікон на грані: темна рамка, світле скло, підвіконня. */
export function Windows({ cx, cy, hx, hy, z = 0, h, side, rows = 1, cols = 2, glass = "#fde68a" }: WindowsProps): ReactElement {
  const width = 0.16;
  const height = Math.min(6, h * 0.28);
  const items: ReactElement[] = [];

  for (let r = 0; r < rows; r++) {
    const zz = z + h * ((r + 1) / (rows + 1)) - height / 2;

    for (let c = 0; c < cols; c++) {
      const t = (c + 1) / (cols + 1);
      const quad = [
        onFace(side, cx, cy, hx, hy, t - width, zz),
        onFace(side, cx, cy, hx, hy, t + width, zz),
        onFace(side, cx, cy, hx, hy, t + width, zz + height),
        onFace(side, cx, cy, hx, hy, t - width, zz + height),
      ];
      const sillA = onFace(side, cx, cy, hx, hy, t - width - 0.03, zz);
      const sillB = onFace(side, cx, cy, hx, hy, t + width + 0.03, zz);

      items.push(
        <g key={`${r}:${c}`}>
          <path d={toPath(quad)} fill={glass} stroke="#1e293b" strokeWidth={0.8} />
          <line x1={sillA.x} y1={sillA.y} x2={sillB.x} y2={sillB.y} stroke="#1e293b" strokeWidth={1.2} />
        </g>,
      );
    }
  }

  return <g>{items}</g>;
}

interface DoorProps {
  cx: number;
  cy: number;
  hx: number;
  hy: number;
  z?: number;
  side: Side;
  /** Позиція вздовж грані 0..1. */
  t?: number;
  h?: number;
  color?: string;
  arched?: boolean;
}

/** Двері біля землі; арка — для кам'яних будівель. */
export function Door({ cx, cy, hx, hy, z = 0, side, t = 0.5, h = 9, color = "#3f2a14", arched = false }: DoorProps): ReactElement {
  const w = 0.14;
  const a = onFace(side, cx, cy, hx, hy, t - w, z);
  const b = onFace(side, cx, cy, hx, hy, t + w, z);
  const c = onFace(side, cx, cy, hx, hy, t + w, z + h);
  const d = onFace(side, cx, cy, hx, hy, t - w, z + h);
  const top = onFace(side, cx, cy, hx, hy, t, z + h + 3);

  const path = arched
    ? `M${a.x} ${a.y} L${b.x} ${b.y} L${c.x} ${c.y} Q${top.x} ${top.y} ${d.x} ${d.y} Z`
    : toPath([a, b, c, d]);

  return <path d={path} fill={color} stroke="#1e293b" strokeWidth={0.8} />;
}

interface TilesProps {
  cx: number;
  cy: number;
  hx: number;
  hy: number;
  z: number;
  h: number;
  /** Гребінь уздовж X (двосхилий) чи шатро (чотирисхилий). */
  kind: "gable" | "pyramid";
  color?: string;
}

/** Ряди черепиці: лінії паралельно карнизу на видимих схилах. */
export function Tiles({ cx, cy, hx, hy, z, h, kind, color = "rgba(15, 23, 42, 0.25)" }: TilesProps): ReactElement {
  const steps = [0.25, 0.5, 0.75];
  const lines: ReactElement[] = [];

  for (const t of steps) {
    if (kind === "gable") {
      const a = at(cx - hx, cy + hy * (1 - t), z + h * t);
      const b = at(cx + hx, cy + hy * (1 - t), z + h * t);
      lines.push(<line key={`l${t}`} x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke={color} strokeWidth={1} />);
    } else {
      const a = at(cx - hx * (1 - t), cy + hy * (1 - t), z + h * t);
      const b = at(cx + hx * (1 - t), cy + hy * (1 - t), z + h * t);
      const c = at(cx + hx * (1 - t), cy - hy * (1 - t), z + h * t);
      lines.push(
        <path key={`p${t}`} d={`M${a.x} ${a.y} L${b.x} ${b.y} L${c.x} ${c.y}`} fill="none" stroke={color} strokeWidth={1} />,
      );
    }
  }

  return <g>{lines}</g>;
}

interface ChimneyProps {
  x: number;
  y: number;
  z: number;
  h?: number;
  faces: Faces;
  smoke?: boolean;
}

/** Димар із клубами диму над ним. */
export function Chimney({ x, y, z, h = 8, faces: f, smoke = true }: ChimneyProps): ReactElement {
  const top = at(x, y, z + h);

  return (
    <g>
      <Box cx={x} cy={y} hx={0.5} hy={0.5} h={h} z={z} faces={f} />
      {smoke && (
        <g fill="rgba(226, 232, 240, 0.85)">
          <circle cx={top.x + 2} cy={top.y - 5} r={3} />
          <circle cx={top.x + 5} cy={top.y - 11} r={4} />
          <circle cx={top.x + 9} cy={top.y - 18} r={5} opacity={0.7} />
        </g>
      )}
    </g>
  );
}

const TRUNK: Faces = { top: "#a16207", left: "#854d0e", right: "#713f12" };
const LEAF: Faces = { top: "#4ade80", left: "#22c55e", right: "#15803d" };
const PINE: Faces = { top: "#16a34a", left: "#15803d", right: "#166534" };

interface TreeProps {
  x: number;
  y: number;
  s?: number;
  /** Хвойне — конус, листяне — куля. */
  kind?: "pine" | "round";
}

/** Дерево: стовбур і крона. Масштаб s тримає його меншим за будівлі. */
export function Tree({ x, y, s = 1, kind = "round" }: TreeProps): ReactElement {
  const trunk = 6 * s;

  if (kind === "pine") {
    return (
      <g>
        <Cylinder cx={x} cy={y} r={0.35 * s} h={trunk} faces={TRUNK} />
        <Cone cx={x} cy={y} r={1.3 * s} z={trunk} h={10 * s} faces={PINE} />
        <Cone cx={x} cy={y} r={1.0 * s} z={trunk + 6 * s} h={9 * s} faces={PINE} />
      </g>
    );
  }

  const crown = at(x, y, trunk + 5 * s);

  return (
    <g>
      <Cylinder cx={x} cy={y} r={0.35 * s} h={trunk} faces={TRUNK} />
      <circle cx={crown.x + 2 * s} cy={crown.y + 1 * s} r={6 * s} fill={LEAF.right} />
      <circle cx={crown.x - 3 * s} cy={crown.y + 1 * s} r={6 * s} fill={LEAF.left} />
      <circle cx={crown.x} cy={crown.y - 3 * s} r={6.5 * s} fill={LEAF.top} />
    </g>
  );
}

interface FenceProps {
  from: { x: number; y: number };
  to: { x: number; y: number };
  step?: number;
  h?: number;
}

/** Паркан: стовпчики з двома перекладинами між ними. */
export function Fence({ from, to, step = 1.2, h = 4 }: FenceProps): ReactElement {
  const length = Math.hypot(to.x - from.x, to.y - from.y);
  const count = Math.max(2, Math.round(length / step) + 1);
  const posts: ReactElement[] = [];
  const rails: ReactElement[] = [];

  for (let i = 0; i < count; i++) {
    const t = i / (count - 1);
    posts.push(
      <Box key={i} cx={from.x + (to.x - from.x) * t} cy={from.y + (to.y - from.y) * t} hx={0.12} hy={0.12} h={h} faces={TRUNK} />,
    );
  }

  for (const zz of [h * 0.35, h * 0.75]) {
    const a = at(from.x, from.y, zz);
    const b = at(to.x, to.y, zz);
    rails.push(<line key={zz} x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke={TRUNK.left} strokeWidth={1.4} />);
  }

  return (
    <g>
      {rails}
      {posts}
    </g>
  );
}

interface BannerProps {
  cx: number;
  cy: number;
  hx: number;
  hy: number;
  z: number;
  side: Side;
  t?: number;
  h?: number;
  color: string;
}

/** Полотнище на стіні: вузький трикутник вістрям донизу. */
export function Banner({ cx, cy, hx, hy, z, side, t = 0.5, h = 10, color }: BannerProps): ReactElement {
  const w = 0.09;
  const a = onFace(side, cx, cy, hx, hy, t - w, z);
  const b = onFace(side, cx, cy, hx, hy, t + w, z);
  const tip = onFace(side, cx, cy, hx, hy, t, z - h);

  return <path d={toPath([a, b, tip])} fill={color} stroke="#1e293b" strokeWidth={0.6} />;
}

interface LanternProps {
  x: number;
  y: number;
  z?: number;
  h?: number;
}

const IRON: Faces = { top: "#475569", left: "#334155", right: "#1e293b" };

/** Ліхтар на стовпі з теплим ореолом. */
export function Lantern({ x, y, z = 0, h = 12 }: LanternProps): ReactElement {
  const top = at(x, y, z + h);

  return (
    <g>
      <Cylinder cx={x} cy={y} r={0.15} z={z} h={h} faces={IRON} />
      <circle cx={top.x} cy={top.y - 2} r={5} fill="#fbbf24" opacity={0.25} />
      <rect x={top.x - 1.8} y={top.y - 5} width={3.6} height={4.5} rx={0.8} fill="#fde68a" stroke="#1e293b" strokeWidth={0.6} />
    </g>
  );
}

interface CrateProps {
  x: number;
  y: number;
  s?: number;
}

const CRATE: Faces = { top: "#d6b98c", left: "#b08a5a", right: "#8a6238" };

/** Ящик або бочка біля складів і крамниць. */
export function Crate({ x, y, s = 1 }: CrateProps): ReactElement {
  return <Box cx={x} cy={y} hx={0.5 * s} hy={0.5 * s} h={4 * s} faces={CRATE} />;
}

export function Barrel({ x, y, s = 1 }: CrateProps): ReactElement {
  return <Cylinder cx={x} cy={y} r={0.45 * s} h={4.5 * s} faces={CRATE} />;
}
