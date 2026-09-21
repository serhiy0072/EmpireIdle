import type { ReactElement } from "react";
import { at, faces, UNIT_X } from "../../lib/iso";
import { Box, Cone, Cylinder, Flag, Gable, Pyramid } from "./isoShapes";
import { spriteFor } from "./sprites";

/**
 * Силует кожної будівлі з примітивів. Плейсхолдер під спрайти:
 * коли з'явиться арт, міняється лише цей файл.
 */
export interface ArtResult {
  node: ReactElement;
  /** Висота над землею в пікселях — над нею висить бульбашка збору. */
  height: number;
  /** Половина сторони на плані — від неї рахується підпис і підсвітка. */
  foot: number;
}

type Art = (cx: number, cy: number, s: number) => ArtResult;

const STONE = faces("#e2e8f0", "#94a3b8", "#64748b");
const WOOD = faces("#e7c9a0", "#b88a5a", "#8a6238");
const WHITE = faces("#f8fafc", "#e2e8f0", "#cbd5e1");
const DARK = faces("#475569", "#334155", "#1e293b");
const ROOF_RED = faces("#f87171", "#dc2626", "#991b1b");
const ROOF_BROWN = faces("#b45309", "#92400e", "#713f12");
const ROOF_BLUE = faces("#93c5fd", "#3b82f6", "#1d4ed8");
const ROOF_PURPLE = faces("#c4b5fd", "#8b5cf6", "#6d28d9");
const ROOF_GOLD = faces("#fde68a", "#f59e0b", "#b45309");
const FIELD = faces("#86efac", "#4ade80", "#16a34a");
const WATER = faces("#bae6fd", "#38bdf8", "#0284c7");
const ROCK_GOLD = faces("#d6c7a1", "#b39b6b", "#8a7446");
const ROCK_IRON = faces("#cbd5e1", "#94a3b8", "#64748b");
const GOLD = faces("#fef08a", "#facc15", "#ca8a04");
const TENT = faces("#fecaca", "#ef4444", "#fef2f2");
const HAY = faces("#fef3c7", "#fcd34d", "#d97706");

const townhall: Art = (cx, cy, s) => {
  const h = 46 * s;
  return {
    foot: 6,
    height: h + 58,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={6} hy={6} h={h} faces={STONE} />
        <Pyramid cx={cx} cy={cy} hx={6} hy={6} z={h} h={34} faces={ROOF_RED} />
        <Flag x={cx} y={cy} z={h + 34} h={22} color="#f59e0b" />
      </g>
    ),
  };
};

const warehouse: Art = (cx, cy, s) => {
  const h = 24 * s;
  return {
    foot: 5,
    height: h + 14,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={3.5} h={h} faces={WOOD} />
        <Gable cx={cx} cy={cy} hx={5} hy={3.5} z={h} h={14} faces={ROOF_BROWN} />
        <Box cx={cx + 1} cy={cy + 3.6} hx={1.2} hy={0.1} h={h * 0.6} faces={DARK} />
      </g>
    ),
  };
};

const farm: Art = (cx, cy, s) => {
  const rows = [-3.5, -1.5, 0.5, 2.5, 4.5];
  const hut = 12 * s;
  return {
    foot: 5,
    height: hut + 8,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={5} h={3} faces={FIELD} />
        {rows.map((row) => {
          const from = at(cx - 4.5, cy + row, 3);
          const to = at(cx + 4.5, cy + row, 3);
          return <line key={row} x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke="#15803d" strokeWidth={2} />;
        })}
        <Box cx={cx + 3} cy={cy - 3} hx={1.6} hy={1.6} h={hut} z={3} faces={WOOD} />
        <Gable cx={cx + 3} cy={cy - 3} hx={1.6} hy={1.6} z={hut + 3} h={8} faces={ROOF_RED} />
      </g>
    ),
  };
};

const sawmill: Art = (cx, cy, s) => {
  const h = 18 * s;
  return {
    foot: 4,
    height: h + 11,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={4} hy={3} h={h} faces={WOOD} />
        <Gable cx={cx} cy={cy} hx={4} hy={3} z={h} h={11} faces={ROOF_RED} />
        <Box cx={cx - 2} cy={cy + 4} hx={2} hy={0.8} h={4} faces={ROOF_BROWN} />
        <Box cx={cx - 2} cy={cy + 4} hx={1.6} hy={0.7} h={3} z={4} faces={ROOF_BROWN} />
      </g>
    ),
  };
};

function mine(rock: typeof ROCK_GOLD, ore: typeof GOLD): Art {
  return (cx, cy, s) => {
    const h = 30 * s;
    return {
      foot: 5,
      height: h,
      node: (
        <g>
          <Pyramid cx={cx} cy={cy} hx={5} hy={5} z={0} h={h} faces={rock} />
          <Box cx={cx + 1} cy={cy + 4.2} hx={1.4} hy={0.3} h={10} faces={DARK} />
          <Box cx={cx + 3.8} cy={cy + 4} hx={0.7} hy={0.7} h={3} faces={ore} />
          <Box cx={cx - 2.5} cy={cy + 4.6} hx={0.6} hy={0.6} h={2.5} faces={ore} />
        </g>
      ),
    };
  };
}

const house: Art = (cx, cy, s) => {
  const h = 18 * s;
  return {
    foot: 3,
    height: h + 14,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3} hy={3} h={h} faces={WOOD} />
        <Box cx={cx + 1.4} cy={cy - 1.6} hx={0.5} hy={0.5} h={h + 14} faces={STONE} />
        <Gable cx={cx} cy={cy} hx={3} hy={3} z={h} h={12} faces={ROOF_RED} />
      </g>
    ),
  };
};

const barracks: Art = (cx, cy, s) => {
  const h = 22 * s;
  const merlons = [-4, -2, 0, 2, 4];
  return {
    foot: 5,
    height: h + 28,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={4} h={h} faces={STONE} />
        {merlons.map((offset) => (
          <Box key={offset} cx={cx + offset} cy={cy + 3.6} hx={0.6} hy={0.4} h={5} z={h} faces={STONE} />
        ))}
        <Flag x={cx - 3} y={cy - 2} z={h} h={24} color="#dc2626" />
        <Flag x={cx + 3} y={cy - 2} z={h} h={24} color="#dc2626" />
      </g>
    ),
  };
};

const bank: Art = (cx, cy, s) => {
  const h = 24 * s;
  const columns = [-3, -1, 1, 3];
  return {
    foot: 4.5,
    height: h + 12,
    node: (
      <g>
        <Box cx={cx} cy={cy - 0.5} hx={4.5} hy={4} h={h} faces={WHITE} />
        {columns.map((offset) => (
          <Box key={offset} cx={cx + offset} cy={cy + 4} hx={0.45} hy={0.45} h={h} faces={WHITE} />
        ))}
        <Pyramid cx={cx} cy={cy} hx={4.8} hy={4.8} z={h} h={12} faces={ROOF_GOLD} />
      </g>
    ),
  };
};

const heroeshall: Art = (cx, cy, s) => {
  const h = 30 * s;
  return {
    foot: 5,
    height: h + 62,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={5} h={h} faces={WHITE} />
        <Pyramid cx={cx} cy={cy} hx={5} hy={5} z={h} h={38} faces={ROOF_PURPLE} />
        <Flag x={cx} y={cy} z={h + 38} h={24} color="#7c3aed" />
      </g>
    ),
  };
};

const lootshop: Art = (cx, cy, s) => {
  const h = 34 * s;
  return {
    foot: 4,
    height: h + 18,
    node: (
      <g>
        <Pyramid cx={cx} cy={cy} hx={4} hy={4} z={0} h={h} faces={TENT} />
        <Flag x={cx} y={cy} z={h} h={18} color="#f59e0b" />
      </g>
    ),
  };
};

const hospital: Art = (cx, cy, s) => {
  const h = 20 * s;
  const cross = faces("#ef4444", "#dc2626", "#b91c1c");
  return {
    foot: 4,
    height: h + 4,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={4} hy={4} h={h} faces={WHITE} />
        <Box cx={cx} cy={cy} hx={2.6} hy={0.7} h={1.5} z={h} faces={cross} />
        <Box cx={cx} cy={cy} hx={0.7} hy={2.6} h={1.5} z={h} faces={cross} />
      </g>
    ),
  };
};

const market: Art = (cx, cy) => {
  const stalls = [
    { dx: -2.5, dy: -2.5, roof: ROOF_RED },
    { dx: 2.5, dy: -2, roof: ROOF_BLUE },
    { dx: -2, dy: 2.5, roof: ROOF_GOLD },
    { dx: 2.5, dy: 2.5, roof: ROOF_PURPLE },
  ];
  return {
    foot: 5,
    height: 18,
    node: (
      <g>
        {stalls.map(({ dx, dy, roof }) => (
          <g key={`${dx}:${dy}`}>
            <Box cx={cx + dx} cy={cy + dy} hx={1.6} hy={1.3} h={8} faces={WOOD} />
            <Gable cx={cx + dx} cy={cy + dy} hx={1.8} hy={1.5} z={8} h={6} faces={roof} />
          </g>
        ))}
      </g>
    ),
  };
};

const stable: Art = (cx, cy, s) => {
  const h = 14 * s;
  const posts = [-4.5, -2.5, -0.5, 1.5, 3.5];
  return {
    foot: 5,
    height: h + 10,
    node: (
      <g>
        <Box cx={cx} cy={cy - 1} hx={5} hy={2.5} h={h} faces={WOOD} />
        <Gable cx={cx} cy={cy - 1} hx={5} hy={2.5} z={h} h={10} faces={ROOF_BROWN} />
        {posts.map((offset) => (
          <Box key={offset} cx={cx + offset} cy={cy + 3.5} hx={0.25} hy={0.25} h={6} faces={WOOD} />
        ))}
        <Box cx={cx - 0.5} cy={cy + 3.5} hx={4.3} hy={0.12} h={1} z={4} faces={WOOD} />
      </g>
    ),
  };
};

const embassy: Art = (cx, cy, s) => {
  const h = 22 * s;
  return {
    foot: 3.5,
    height: h + 46,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} faces={WHITE} />
        <Pyramid cx={cx} cy={cy} hx={3.5} hy={3.5} z={h} h={16} faces={ROOF_BLUE} />
        <Flag x={cx + 2.8} y={cy + 2.8} z={0} h={h + 30} color="#2563eb" />
      </g>
    ),
  };
};

const scouttower: Art = (cx, cy, s) => {
  const h = 56 * s;
  return {
    foot: 2,
    height: h + 18,
    node: (
      <g>
        <Cylinder cx={cx} cy={cy} r={1.6} h={h} faces={STONE} />
        <Cone cx={cx} cy={cy} r={2.2} z={h} h={18} faces={ROOF_RED} />
      </g>
    ),
  };
};

const forge: Art = (cx, cy, s) => {
  const h = 18 * s;
  const chimney = h + 24;
  const glow = at(cx + 2, cy - 2, chimney);
  return {
    foot: 3.5,
    height: chimney,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} faces={DARK} />
        <Cylinder cx={cx + 2} cy={cy - 2} r={0.8} h={chimney} faces={STONE} />
        <Gable cx={cx} cy={cy} hx={3.5} hy={3.5} z={h} h={12} faces={ROOF_BROWN} />
        <ellipse cx={glow.x} cy={glow.y} rx={10} ry={5} fill="#fb923c" opacity={0.9} />
      </g>
    ),
  };
};

const fishinghut: Art = (cx, cy, s) => {
  const h = 12 * s;
  return {
    foot: 5,
    height: h + 9,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={4} h={2} faces={WATER} />
        <Box cx={cx + 2} cy={cy + 1} hx={2.5} hy={0.6} h={1} z={2} faces={WOOD} />
        <Box cx={cx - 1.5} cy={cy - 1} hx={2} hy={2} h={h} z={2} faces={WOOD} />
        <Gable cx={cx - 1.5} cy={cy - 1} hx={2} hy={2} z={h + 2} h={9} faces={ROOF_BLUE} />
      </g>
    ),
  };
};

const beastpen: Art = (cx, cy) => ({
  foot: 4.5,
  height: 12,
  node: (
    <g>
      <Box cx={cx} cy={cy - 4.5} hx={4.5} hy={0.2} h={6} faces={WOOD} />
      <Box cx={cx - 4.5} cy={cy} hx={0.2} hy={4.5} h={6} faces={WOOD} />
      <Cylinder cx={cx - 1} cy={cy - 1} r={1.3} h={5} faces={HAY} />
      <Cylinder cx={cx + 1.5} cy={cy + 1} r={1.1} h={4} faces={HAY} />
      <Box cx={cx + 4.5} cy={cy} hx={0.2} hy={4.5} h={6} faces={WOOD} />
      <Box cx={cx} cy={cy + 4.5} hx={4.5} hy={0.2} h={6} faces={WOOD} />
    </g>
  ),
});

const fallback: Art = (cx, cy, s) => {
  const h = 20 * s;
  return {
    foot: 3.5,
    height: h + 12,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} faces={STONE} />
        <Gable cx={cx} cy={cy} hx={3.5} hy={3.5} z={h} h={12} faces={ROOF_BLUE} />
      </g>
    ),
  };
};

const ART: Record<string, Art> = {
  townhall,
  warehouse,
  farm,
  sawmill,
  goldmine: mine(ROCK_GOLD, GOLD),
  ironmine: mine(ROCK_IRON, STONE),
  house,
  barracks,
  bank,
  heroeshall,
  lootshop,
  hospital,
  market,
  stable,
  embassy,
  scouttower,
  forge,
  fishinghut,
  beastpen,
};

/** Нова будівля в конфізі без власного силуету отримує типовий будинок, а не падіння. */
export function artFor(buildingKey: string): Art {
  return ART[buildingKey] ?? fallback;
}

/** Половина сторони спрайтової будівлі на плані — як у більшості силуетів. */
const SPRITE_FOOT = 4;

/**
 * Арт будівлі: спрайт із маніфесту, якщо є, інакше процедурний силует.
 * Спрайт масштабується до ширини ромба основи, якір — його нижня вершина
 * (див. public/sprites/README.md). Рівень росте так само, як і в силуетів.
 */
export function buildingArt(buildingKey: string, level: number, cx: number, cy: number): ArtResult {
  const sprite = spriteFor(buildingKey, level);
  const s = buildingScale(level);

  if (sprite === null) {
    return artFor(buildingKey)(cx, cy, s);
  }

  const width = 4 * SPRITE_FOOT * UNIT_X * s;
  const height = width * sprite.aspect;
  const anchor = at(cx + SPRITE_FOOT, cy + SPRITE_FOOT);

  return {
    foot: SPRITE_FOOT,
    // Бульбашка збору висить трохи нижче верху PNG: угорі зазвичай прозорий запас
    height: height * 0.8,
    node: (
      <image
        href={sprite.href}
        x={anchor.x - width / 2}
        y={anchor.y - height}
        width={width}
        height={height}
        preserveAspectRatio="xMidYMax meet"
      />
    ),
  };
}

/** Будівля трохи росте з рівнем, але не безмежно — інакше закриє сусідів. */
export function buildingScale(level: number): number {
  return 1 + Math.min(level, 30) * 0.015;
}