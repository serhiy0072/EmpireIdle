import type { ReactElement } from "react";
import { at, faces, UNIT_X, type Faces } from "../../lib/iso";
import { Banner, Barrel, Chimney, Crate, Door, Fence, Lantern, Tiles, Tree, Windows } from "./isoDetails";
import { Box, Cone, Cylinder, Flag, Gable, Pyramid } from "./isoShapes";
import { spriteFor, tierOf, type Tier } from "./sprites";

/**
 * Силует кожної будівлі з примітивів. Плейсхолдер під спрайти:
 * коли з'явиться арт, міняється лише маніфест sprites.ts.
 */
export interface ArtResult {
  node: ReactElement;
  /** Висота над землею в пікселях — над нею висить бульбашка збору. */
  height: number;
  /** Половина сторони на плані — від неї рахується підпис і підсвітка. */
  foot: number;
}

type Art = (cx: number, cy: number, s: number, tier: Tier) => ArtResult;

const STONE = faces("#e2e8f0", "#94a3b8", "#64748b");
const WOOD = faces("#e7c9a0", "#b88a5a", "#8a6238");
const WHITE = faces("#f8fafc", "#e2e8f0", "#cbd5e1");
const DARK = faces("#475569", "#334155", "#1e293b");
const ROOF_RED = faces("#f87171", "#dc2626", "#991b1b");
const ROOF_BROWN = faces("#b45309", "#92400e", "#713f12");
const ROOF_BLUE = faces("#93c5fd", "#3b82f6", "#1d4ed8");
const ROOF_PURPLE = faces("#c4b5fd", "#8b5cf6", "#6d28d9");
const ROOF_GOLD = faces("#fde68a", "#f59e0b", "#b45309");
const ROOF_SLATE = faces("#94a3b8", "#64748b", "#475569");
const FIELD = faces("#86efac", "#4ade80", "#16a34a");
const SOIL = faces("#a16207", "#854d0e", "#713f12");
const WATER = faces("#bae6fd", "#38bdf8", "#0284c7");
const ROCK_GOLD = faces("#d6c7a1", "#b39b6b", "#8a7446");
const ROCK_IRON = faces("#cbd5e1", "#94a3b8", "#64748b");
const GOLD = faces("#fef08a", "#facc15", "#ca8a04");
const IRON = faces("#e2e8f0", "#94a3b8", "#475569");
const TENT = faces("#fecaca", "#ef4444", "#b91c1c");
const TENT_STRIPE = faces("#fef2f2", "#fecaca", "#fca5a5");
const HAY = faces("#fef3c7", "#fcd34d", "#d97706");
const BRICK = faces("#fca5a5", "#dc2626", "#991b1b");

/** Барва прапорців за тіром: тканина → синь → золото. */
const TIER_FLAG = ["#dc2626", "#2563eb", "#f59e0b"] as const;

/** Золотий кант на карнизі — знак 10+ рівня. */
function trim(cx: number, cy: number, hx: number, hy: number, z: number): ReactElement {
  const a = at(cx - hx, cy + hy, z);
  const b = at(cx + hx, cy + hy, z);
  const c = at(cx + hx, cy - hy, z);

  return <path d={`M${a.x} ${a.y} L${b.x} ${b.y} L${c.x} ${c.y}`} fill="none" stroke="#f59e0b" strokeWidth={1.6} />;
}

const townhall: Art = (cx, cy, s, tier) => {
  const h = 46 * s;
  const roof = 30;
  const towerH = h + 18;
  return {
    foot: 6,
    height: towerH + 40,
    node: (
      <g>
        {/* Кам'яний цоколь, на ньому головний корпус */}
        <Box cx={cx} cy={cy} hx={6.4} hy={6.4} h={4} faces={ROOF_SLATE} />
        <Box cx={cx} cy={cy} hx={6} hy={6} h={h} z={4} faces={STONE} />
        <Windows cx={cx} cy={cy} hx={6} hy={6} z={4} h={h} side="left" rows={2} cols={3} />
        <Windows cx={cx} cy={cy} hx={6} hy={6} z={4} h={h} side="right" rows={2} cols={3} />
        <Door cx={cx} cy={cy} hx={6} hy={6} z={4} side="left" h={12} arched />
        <Pyramid cx={cx} cy={cy} hx={6.4} hy={6.4} z={h + 4} h={roof} faces={ROOF_RED} />
        <Tiles cx={cx} cy={cy} hx={6.4} hy={6.4} z={h + 4} h={roof} kind="pyramid" />
        {/* Кутові вежі: у тірі 1+ — з обох боків */}
        <Cylinder cx={cx - 5.2} cy={cy + 5.2} r={1.3} h={towerH} faces={STONE} />
        <Cone cx={cx - 5.2} cy={cy + 5.2} r={1.7} z={towerH} h={12} faces={ROOF_RED} />
        {tier >= 1 && (
          <>
            <Cylinder cx={cx + 5.2} cy={cy + 5.2} r={1.3} h={towerH} faces={STONE} />
            <Cone cx={cx + 5.2} cy={cy + 5.2} r={1.7} z={towerH} h={12} faces={ROOF_RED} />
          </>
        )}
        <Banner cx={cx} cy={cy} hx={6} hy={6} z={h - 2} side="left" t={0.28} color={TIER_FLAG[tier]} />
        <Banner cx={cx} cy={cy} hx={6} hy={6} z={h - 2} side="left" t={0.72} color={TIER_FLAG[tier]} />
        <Flag x={cx} y={cy} z={h + 4 + roof} h={22} color={TIER_FLAG[tier]} />
        {tier >= 2 && trim(cx, cy, 6.4, 6.4, h + 4)}
        <Lantern x={cx - 2.5} y={cy + 7.2} />
        <Lantern x={cx + 2.5} y={cy + 7.2} />
      </g>
    ),
  };
};

const warehouse: Art = (cx, cy, s, tier) => {
  const h = 24 * s;
  return {
    foot: 5,
    height: h + 14,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={3.5} h={h} faces={WOOD} />
        <Door cx={cx} cy={cy} hx={5} hy={3.5} side="left" t={0.6} h={h * 0.6} />
        <Windows cx={cx} cy={cy} hx={5} hy={3.5} h={h} side="left" cols={1} glass="#cbd5e1" />
        <Windows cx={cx} cy={cy} hx={5} hy={3.5} h={h} side="right" cols={2} glass="#cbd5e1" />
        <Gable cx={cx} cy={cy} hx={5.4} hy={3.9} z={h} h={14} faces={ROOF_BROWN} />
        <Tiles cx={cx} cy={cy} hx={5.4} hy={3.9} z={h} h={14} kind="gable" />
        <Crate x={cx - 4} y={cy + 4.6} />
        <Crate x={cx - 3} y={cy + 4.6} s={0.8} />
        <Barrel x={cx + 6} y={cy + 1} />
        {tier >= 1 && <Barrel x={cx + 6} y={cy - 1} />}
        {tier >= 2 && <Box cx={cx - 3.5} cy={cy + 4.6} hx={0.5} hy={0.5} h={4} z={4} faces={GOLD} />}
      </g>
    ),
  };
};

const farm: Art = (cx, cy, s, tier) => {
  const rows = [-3.5, -2, -0.5, 1, 2.5, 4];
  const hut = 12 * s;
  return {
    foot: 5,
    height: hut + 12,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={5} h={2} faces={SOIL} />
        {rows.map((row) => {
          const from = at(cx - 4.5, cy + row, 2);
          const to = at(cx + 4.5, cy + row, 2);
          return (
            <g key={row}>
              <line x1={from.x} y1={from.y} x2={to.x} y2={to.y} stroke="#15803d" strokeWidth={3} strokeLinecap="round" />
              <line x1={from.x} y1={from.y - 1.5} x2={to.x} y2={to.y - 1.5} stroke="#4ade80" strokeWidth={1.5} strokeLinecap="round" />
            </g>
          );
        })}
        <Fence from={{ x: cx - 5, y: cy + 5 }} to={{ x: cx + 5, y: cy + 5 }} />
        <Fence from={{ x: cx + 5, y: cy + 5 }} to={{ x: cx + 5, y: cy - 5 }} />
        <Box cx={cx + 3} cy={cy - 3} hx={1.8} hy={1.8} h={hut} z={2} faces={WOOD} />
        <Door cx={cx + 3} cy={cy - 3} hx={1.8} hy={1.8} z={2} side="left" h={7} />
        <Gable cx={cx + 3} cy={cy - 3} hx={2.1} hy={2.1} z={hut + 2} h={9} faces={ROOF_RED} />
        <Tiles cx={cx + 3} cy={cy - 3} hx={2.1} hy={2.1} z={hut + 2} h={9} kind="gable" />
        <Cylinder cx={cx - 3.5} cy={cy - 3.5} r={1} z={2} h={5} faces={HAY} />
        <Cone cx={cx - 3.5} cy={cy - 3.5} r={1.1} z={7} h={4} faces={HAY} />
        {tier >= 1 && (
          <>
            <Cylinder cx={cx - 1.5} cy={cy - 4} r={0.9} z={2} h={4} faces={HAY} />
            <Cone cx={cx - 1.5} cy={cy - 4} r={1} z={6} h={3.5} faces={FIELD} />
          </>
        )}
        {tier >= 2 && <Flag x={cx + 3} y={cy - 3} z={hut + 11} h={10} color={TIER_FLAG[2]} />}
      </g>
    ),
  };
};

const sawmill: Art = (cx, cy, s, tier) => {
  const h = 18 * s;
  const blade = at(cx + 4.05, cy - 1, h * 0.55);
  return {
    foot: 4,
    height: h + 11,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={4} hy={3} h={h} faces={WOOD} />
        <Door cx={cx} cy={cy} hx={4} hy={3} side="left" t={0.3} h={h * 0.65} />
        <Windows cx={cx} cy={cy} hx={4} hy={3} h={h} side="left" cols={1} />
        {/* Пилкове коло на правій стіні */}
        <circle cx={blade.x} cy={blade.y} r={5} fill="#cbd5e1" stroke="#1e293b" strokeWidth={0.8} />
        <circle cx={blade.x} cy={blade.y} r={1.2} fill="#1e293b" />
        <Gable cx={cx} cy={cy} hx={4.4} hy={3.4} z={h} h={11} faces={ROOF_RED} />
        <Tiles cx={cx} cy={cy} hx={4.4} hy={3.4} z={h} h={11} kind="gable" />
        <Chimney x={cx - 2.5} y={cy - 1.5} z={h + 4} faces={STONE} />
        {/* Штабель колод */}
        <Cylinder cx={cx - 1.5} cy={cy + 4.5} r={0.45} h={1} faces={ROOF_BROWN} />
        <Cylinder cx={cx - 0.5} cy={cy + 4.5} r={0.45} h={1} faces={ROOF_BROWN} />
        <Cylinder cx={cx - 1} cy={cy + 4.5} r={0.45} z={1} h={1} faces={ROOF_BROWN} />
        <Tree x={cx + 5} y={cy + 4} s={0.7} kind="pine" />
        {tier >= 1 && <Tree x={cx - 5} y={cy + 4.5} s={0.6} kind="pine" />}
        {tier >= 2 && trim(cx, cy, 4.4, 3.4, h)}
      </g>
    ),
  };
};

function mine(rock: Faces, ore: Faces): Art {
  return (cx, cy, s, tier) => {
    const h = 30 * s;
    const entrance = at(cx + 1, cy + 5.02, 0);
    return {
      foot: 5,
      height: h,
      node: (
        <g>
          <Pyramid cx={cx} cy={cy} hx={5} hy={5} z={0} h={h} faces={rock} />
          {/* Вхід у штольню з дерев'яною рамою */}
          <path
            d={`M${entrance.x - 8} ${entrance.y} L${entrance.x - 8} ${entrance.y - 9} Q${entrance.x} ${entrance.y - 15} ${entrance.x + 8} ${entrance.y - 9} L${entrance.x + 8} ${entrance.y} Z`}
            fill="#1e293b"
            stroke="#8a6238"
            strokeWidth={2}
          />
          {/* Рейки з входу */}
          <line x1={entrance.x - 3} y1={entrance.y} x2={entrance.x - 6} y2={entrance.y + 8} stroke="#475569" strokeWidth={1.2} />
          <line x1={entrance.x + 3} y1={entrance.y} x2={entrance.x} y2={entrance.y + 8} stroke="#475569" strokeWidth={1.2} />
          {/* Вагонетка */}
          <Box cx={cx + 0.4} cy={cy + 6.4} hx={0.7} hy={0.5} h={2.5} faces={DARK} />
          <Box cx={cx + 0.4} cy={cy + 6.4} hx={0.5} hy={0.35} h={1.5} z={2.5} faces={ore} />
          <Box cx={cx + 3.8} cy={cy + 4} hx={0.7} hy={0.7} h={3} faces={ore} />
          <Box cx={cx - 2.5} cy={cy + 4.6} hx={0.6} hy={0.6} h={2.5} faces={ore} />
          {tier >= 1 && <Box cx={cx - 4} cy={cy + 3} hx={0.5} hy={0.5} h={2} faces={ore} />}
          <Lantern x={cx + 2.6} y={cy + 5.4} h={9} />
          {tier >= 2 && <Flag x={cx} y={cy} z={h} h={12} color={TIER_FLAG[2]} />}
        </g>
      ),
    };
  };
}

const house: Art = (cx, cy, s, tier) => {
  const h = 18 * s;
  return {
    foot: 3,
    height: h + 16,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3} hy={3} h={h} faces={tier >= 2 ? STONE : WOOD} />
        <Windows cx={cx} cy={cy} hx={3} hy={3} h={h} side="left" cols={1} />
        <Windows cx={cx} cy={cy} hx={3} hy={3} h={h} side="right" cols={1} />
        <Door cx={cx} cy={cy} hx={3} hy={3} side="left" t={0.7} h={8} />
        <Gable cx={cx} cy={cy} hx={3.4} hy={3.4} z={h} h={12} faces={ROOF_RED} />
        <Tiles cx={cx} cy={cy} hx={3.4} hy={3.4} z={h} h={12} kind="gable" />
        <Chimney x={cx + 1.6} y={cy - 1.6} z={h + 5} h={7} faces={STONE} />
        <Tree x={cx - 4} y={cy + 3.5} s={0.6} />
        {tier >= 1 && <Fence from={{ x: cx - 3.5, y: cy + 4 }} to={{ x: cx + 3.5, y: cy + 4 }} h={3} />}
      </g>
    ),
  };
};

const barracks: Art = (cx, cy, s, tier) => {
  const h = 22 * s;
  const merlons = [-4, -2, 0, 2, 4];
  const towerH = h + 8;
  return {
    foot: 5,
    height: towerH + 26,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={4} h={h} faces={STONE} />
        <Door cx={cx} cy={cy} hx={5} hy={4} side="left" h={11} arched color="#1e293b" />
        <Windows cx={cx} cy={cy} hx={5} hy={4} h={h} side="left" cols={2} glass="#1e293b" />
        <Windows cx={cx} cy={cy} hx={5} hy={4} h={h} side="right" cols={2} glass="#1e293b" />
        {merlons.map((offset) => (
          <Box key={offset} cx={cx + offset} cy={cy + 3.6} hx={0.6} hy={0.4} h={5} z={h} faces={STONE} />
        ))}
        {merlons.map((offset) => (
          <Box key={`r${offset}`} cx={cx + 4.6} cy={cy + offset * 0.8} hx={0.4} hy={0.5} h={5} z={h} faces={STONE} />
        ))}
        <Cylinder cx={cx - 4.4} cy={cy + 3.4} r={1.2} h={towerH} faces={STONE} />
        <Cone cx={cx - 4.4} cy={cy + 3.4} r={1.5} z={towerH} h={8} faces={ROOF_SLATE} />
        <Flag x={cx - 4.4} y={cy + 3.4} z={towerH + 8} h={16} color={TIER_FLAG[tier]} />
        <Flag x={cx + 3} y={cy - 2} z={h} h={22} color={TIER_FLAG[tier]} />
        {/* Тренувальні опудала на подвір'ї */}
        <Cylinder cx={cx + 6} cy={cy + 2} r={0.25} h={5} faces={WOOD} />
        <Cylinder cx={cx + 6} cy={cy + 2} r={0.7} z={5} h={2.5} faces={HAY} />
        {tier >= 1 && (
          <>
            <Cylinder cx={cx + 6} cy={cy - 0.5} r={0.25} h={5} faces={WOOD} />
            <Cylinder cx={cx + 6} cy={cy - 0.5} r={0.7} z={5} h={2.5} faces={HAY} />
          </>
        )}
        {tier >= 2 && <Banner cx={cx} cy={cy} hx={5} hy={4} z={h - 1} side="left" t={0.75} h={12} color={TIER_FLAG[2]} />}
      </g>
    ),
  };
};

const bank: Art = (cx, cy, s, tier) => {
  const h = 24 * s;
  const columns = [-3, -1, 1, 3];
  return {
    foot: 4.5,
    height: h + 16,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={4.6} h={3} faces={ROOF_SLATE} />
        <Box cx={cx} cy={cy - 0.5} hx={4.5} hy={4} h={h} z={3} faces={WHITE} />
        <Windows cx={cx} cy={cy - 0.5} hx={4.5} hy={4} z={3} h={h} side="right" cols={2} glass="#bfdbfe" />
        <Door cx={cx} cy={cy - 0.5} hx={4.5} hy={4} z={3} side="left" h={12} color="#1e3a8a" arched />
        {columns.map((offset) => (
          <Cylinder key={offset} cx={cx + offset} cy={cy + 4} r={0.45} z={3} h={h} faces={WHITE} />
        ))}
        <Box cx={cx} cy={cy + 4} hx={4.6} hy={0.6} h={2} z={h + 3} faces={WHITE} />
        <Pyramid cx={cx} cy={cy} hx={4.9} hy={4.9} z={h + 5} h={12} faces={ROOF_GOLD} />
        <Tiles cx={cx} cy={cy} hx={4.9} hy={4.9} z={h + 5} h={12} kind="pyramid" />
        {tier >= 1 && <Box cx={cx} cy={cy} hx={0.8} hy={0.8} h={4} z={h + 17} faces={GOLD} />}
        {tier >= 2 && trim(cx, cy, 5, 4.6, 3)}
        <Lantern x={cx - 4} y={cy + 5.5} h={10} />
        <Lantern x={cx + 4} y={cy + 5.5} h={10} />
      </g>
    ),
  };
};

const heroeshall: Art = (cx, cy, s, tier) => {
  const h = 30 * s;
  const roof = 34;
  return {
    foot: 5,
    height: h + roof + 26,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={5} h={h} faces={WHITE} />
        <Windows cx={cx} cy={cy} hx={5} hy={5} h={h} side="left" rows={2} cols={2} glass="#c4b5fd" />
        <Windows cx={cx} cy={cy} hx={5} hy={5} h={h} side="right" rows={2} cols={2} glass="#c4b5fd" />
        <Door cx={cx} cy={cy} hx={5} hy={5} side="left" h={13} arched color="#4c1d95" />
        <Banner cx={cx} cy={cy} hx={5} hy={5} z={h - 2} side="left" t={0.2} h={14} color="#7c3aed" />
        <Banner cx={cx} cy={cy} hx={5} hy={5} z={h - 2} side="left" t={0.8} h={14} color="#7c3aed" />
        <Pyramid cx={cx} cy={cy} hx={5.4} hy={5.4} z={h} h={roof} faces={ROOF_PURPLE} />
        <Tiles cx={cx} cy={cy} hx={5.4} hy={5.4} z={h} h={roof} kind="pyramid" />
        <Flag x={cx} y={cy} z={h + roof} h={24} color={tier >= 2 ? TIER_FLAG[2] : "#7c3aed"} />
        {/* Статуї героїв обабіч входу */}
        <Box cx={cx - 2} cy={cy + 6} hx={0.6} hy={0.6} h={3} faces={STONE} />
        <Cylinder cx={cx - 2} cy={cy + 6} r={0.4} z={3} h={7} faces={STONE} />
        {tier >= 1 && (
          <>
            <Box cx={cx + 2} cy={cy + 6} hx={0.6} hy={0.6} h={3} faces={STONE} />
            <Cylinder cx={cx + 2} cy={cy + 6} r={0.4} z={3} h={7} faces={STONE} />
          </>
        )}
        {tier >= 2 && trim(cx, cy, 5.4, 5.4, h)}
      </g>
    ),
  };
};

const lootshop: Art = (cx, cy, s, tier) => {
  const h = 34 * s;
  return {
    foot: 4,
    height: h + 18,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={4.2} hy={4.2} h={2} faces={WOOD} />
        <Pyramid cx={cx} cy={cy} hx={4} hy={4} z={2} h={h} faces={TENT} />
        {/* Смуги намету */}
        <Pyramid cx={cx - 1.4} cy={cy + 1.4} hx={0.9} hy={0.9} z={2} h={h * 0.62} faces={TENT_STRIPE} />
        <Pyramid cx={cx + 1.4} cy={cy + 1.4} hx={0.9} hy={0.9} z={2} h={h * 0.62} faces={TENT_STRIPE} />
        <Door cx={cx} cy={cy} hx={4} hy={4} z={2} side="left" h={12} color="#7f1d1d" arched />
        <Flag x={cx} y={cy} z={h + 2} h={18} color={TIER_FLAG[tier]} />
        <Crate x={cx + 5} y={cy + 2} />
        <Barrel x={cx + 5} y={cy} />
        {tier >= 1 && <Box cx={cx - 5} cy={cy + 2} hx={0.6} hy={0.6} h={3} faces={GOLD} />}
        <Lantern x={cx - 4.8} y={cy + 4.8} h={10} />
      </g>
    ),
  };
};

const hospital: Art = (cx, cy, s, tier) => {
  const h = 20 * s;
  const cross = faces("#ef4444", "#dc2626", "#b91c1c");
  return {
    foot: 4,
    height: h + 14,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={4} hy={4} h={h} faces={WHITE} />
        <Windows cx={cx} cy={cy} hx={4} hy={4} h={h} side="left" cols={3} glass="#bfdbfe" />
        <Windows cx={cx} cy={cy} hx={4} hy={4} h={h} side="right" cols={2} glass="#bfdbfe" />
        <Door cx={cx} cy={cy} hx={4} hy={4} side="left" h={10} color="#f1f5f9" />
        <Gable cx={cx} cy={cy} hx={4.4} hy={4.4} z={h} h={10} faces={ROOF_SLATE} />
        <Tiles cx={cx} cy={cy} hx={4.4} hy={4.4} z={h} h={10} kind="gable" />
        <Box cx={cx} cy={cy} hx={2.2} hy={0.6} h={1.5} z={h + 10} faces={cross} />
        <Box cx={cx} cy={cy} hx={0.6} hy={2.2} h={1.5} z={h + 10} faces={cross} />
        <Chimney x={cx - 2.8} y={cy - 2.8} z={h + 3} h={7} faces={STONE} smoke={tier >= 1} />
        <Tree x={cx + 5.5} y={cy + 3} s={0.7} />
        {tier >= 2 && <Tree x={cx - 5.5} y={cy + 3} s={0.7} />}
      </g>
    ),
  };
};

const market: Art = (cx, cy, _s, tier) => {
  const stalls = [
    { dx: -2.5, dy: -2.5, roof: ROOF_RED, goods: GOLD },
    { dx: 2.5, dy: -2, roof: ROOF_BLUE, goods: FIELD },
    { dx: -2, dy: 2.5, roof: ROOF_GOLD, goods: HAY },
    { dx: 2.5, dy: 2.5, roof: ROOF_PURPLE, goods: IRON },
  ];
  return {
    foot: 5,
    height: 22,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5.5} hy={5.5} h={1} faces={ROOF_SLATE} />
        {stalls.map(({ dx, dy, roof, goods }) => (
          <g key={`${dx}:${dy}`}>
            <Box cx={cx + dx} cy={cy + dy} hx={1.6} hy={1.3} h={5} z={1} faces={WOOD} />
            <Box cx={cx + dx} cy={cy + dy} hx={1.2} hy={0.9} h={2} z={6} faces={goods} />
            <Cylinder cx={cx + dx - 1.5} cy={cy + dy + 1.2} r={0.15} z={1} h={11} faces={WOOD} />
            <Cylinder cx={cx + dx + 1.5} cy={cy + dy + 1.2} r={0.15} z={1} h={11} faces={WOOD} />
            <Gable cx={cx + dx} cy={cy + dy} hx={1.9} hy={1.6} z={12} h={5} faces={roof} />
          </g>
        ))}
        {/* Фонтан посередині */}
        <Cylinder cx={cx} cy={cy} r={1.1} z={1} h={2} faces={STONE} />
        <Cylinder cx={cx} cy={cy} r={0.8} z={3} h={1} faces={WATER} />
        {tier >= 1 && <Cylinder cx={cx} cy={cy} r={0.25} z={4} h={6} faces={STONE} />}
        {tier >= 2 && <Box cx={cx} cy={cy} hx={0.5} hy={0.5} h={2} z={10} faces={GOLD} />}
        <Lantern x={cx} y={cy + 5.8} h={10} />
      </g>
    ),
  };
};

const stable: Art = (cx, cy, s, tier) => {
  const h = 14 * s;
  const posts = [-4.5, -2.5, -0.5, 1.5, 3.5];
  return {
    foot: 5,
    height: h + 12,
    node: (
      <g>
        <Box cx={cx} cy={cy - 1} hx={5} hy={2.5} h={h} faces={WOOD} />
        <Door cx={cx} cy={cy - 1} hx={5} hy={2.5} side="left" t={0.25} h={h * 0.75} />
        <Door cx={cx} cy={cy - 1} hx={5} hy={2.5} side="left" t={0.75} h={h * 0.75} />
        <Gable cx={cx} cy={cy - 1} hx={5.4} hy={2.9} z={h} h={10} faces={ROOF_BROWN} />
        <Tiles cx={cx} cy={cy - 1} hx={5.4} hy={2.9} z={h} h={10} kind="gable" />
        {posts.map((offset) => (
          <Box key={offset} cx={cx + offset} cy={cy + 3.5} hx={0.25} hy={0.25} h={6} faces={WOOD} />
        ))}
        <Box cx={cx - 0.5} cy={cy + 3.5} hx={4.3} hy={0.12} h={1} z={4} faces={WOOD} />
        <Fence from={{ x: cx - 4.5, y: cy + 3.5 }} to={{ x: cx - 4.5, y: cy + 6 }} />
        <Fence from={{ x: cx + 3.5, y: cy + 3.5 }} to={{ x: cx + 3.5, y: cy + 6 }} />
        {/* Копиця й корито */}
        <Cylinder cx={cx - 2.5} cy={cy + 5} r={0.9} h={3.5} faces={HAY} />
        <Box cx={cx + 1.5} cy={cy + 5} hx={1.2} hy={0.4} h={1.5} faces={WOOD} />
        {tier >= 1 && <Cylinder cx={cx + 6} cy={cy + 1} r={0.9} h={3.5} faces={HAY} />}
        {tier >= 2 && <Flag x={cx + 5} y={cy - 3.5} z={h} h={14} color={TIER_FLAG[2]} />}
      </g>
    ),
  };
};

const embassy: Art = (cx, cy, s, tier) => {
  const h = 22 * s;
  return {
    foot: 3.5,
    height: h + 46,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} faces={WHITE} />
        <Windows cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} side="left" rows={2} cols={2} glass="#bfdbfe" />
        <Windows cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} side="right" rows={2} cols={1} glass="#bfdbfe" />
        <Door cx={cx} cy={cy} hx={3.5} hy={3.5} side="left" h={10} arched color="#1e3a8a" />
        <Pyramid cx={cx} cy={cy} hx={3.9} hy={3.9} z={h} h={16} faces={ROOF_BLUE} />
        <Tiles cx={cx} cy={cy} hx={3.9} hy={3.9} z={h} h={16} kind="pyramid" />
        <Flag x={cx + 2.8} y={cy + 2.8} z={0} h={h + 30} color="#2563eb" />
        {tier >= 1 && <Flag x={cx - 2.8} y={cy + 2.8} z={0} h={h + 26} color={TIER_FLAG[tier]} />}
        <Banner cx={cx} cy={cy} hx={3.5} hy={3.5} z={h - 1} side="left" t={0.5} h={10} color="#2563eb" />
        <Lantern x={cx + 4.5} y={cy + 1} h={10} />
        {tier >= 2 && trim(cx, cy, 3.9, 3.9, h)}
      </g>
    ),
  };
};

const scouttower: Art = (cx, cy, s, tier) => {
  const h = 56 * s;
  const deck = at(cx, cy, h);
  return {
    foot: 2,
    height: h + 20,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={2.2} hy={2.2} h={3} faces={ROOF_SLATE} />
        <Cylinder cx={cx} cy={cy} r={1.6} z={3} h={h - 3} faces={STONE} />
        <Windows cx={cx} cy={cy} hx={1.6} hy={1.6} z={3} h={h - 3} side="left" rows={3} cols={1} glass="#1e293b" />
        <Door cx={cx} cy={cy} hx={1.6} hy={1.6} z={3} side="left" h={9} arched />
        {/* Оглядовий майданчик ширший за вежу */}
        <Cylinder cx={cx} cy={cy} r={2.3} z={h} h={2} faces={WOOD} />
        <Cone cx={cx} cy={cy} r={2.4} z={h + 2} h={16} faces={ROOF_RED} />
        <Flag x={cx} y={cy} z={h + 18} h={12} color={TIER_FLAG[tier]} />
        {/* Сигнальне вогнище на тірі 1+ */}
        {tier >= 1 && <circle cx={deck.x + 10} cy={deck.y - 4} r={3} fill="#fb923c" opacity={0.9} />}
      </g>
    ),
  };
};

const forge: Art = (cx, cy, s, tier) => {
  const h = 18 * s;
  const chimney = h + 24;
  const glow = at(cx + 3.55, cy + 0.5, h * 0.35);
  return {
    foot: 3.5,
    height: chimney,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} faces={BRICK} />
        <Door cx={cx} cy={cy} hx={3.5} hy={3.5} side="left" h={10} color="#1e293b" arched />
        {/* Відкрите горно на правій стіні світиться */}
        <ellipse cx={glow.x} cy={glow.y} rx={6} ry={4} fill="#fb923c" opacity={0.9} />
        <circle cx={glow.x} cy={glow.y} r={2.5} fill="#fde68a" />
        <Gable cx={cx} cy={cy} hx={3.9} hy={3.9} z={h} h={12} faces={ROOF_SLATE} />
        <Tiles cx={cx} cy={cy} hx={3.9} hy={3.9} z={h} h={12} kind="gable" />
        <Chimney x={cx + 2} y={cy - 2} z={h + 4} h={chimney - h - 4} faces={DARK} />
        {/* Ковадло надворі */}
        <Box cx={cx - 1.5} cy={cy + 5} hx={0.5} hy={0.5} h={2} faces={WOOD} />
        <Box cx={cx - 1.5} cy={cy + 5} hx={0.9} hy={0.35} h={1.2} z={2} faces={DARK} />
        <Barrel x={cx + 5} y={cy + 1} />
        {tier >= 1 && <Box cx={cx + 5} cy={cy - 1} hx={0.5} hy={0.5} h={3} faces={IRON} />}
        {tier >= 2 && <Flag x={cx - 2.5} y={cy - 2.5} z={h + 10} h={12} color={TIER_FLAG[2]} />}
      </g>
    ),
  };
};

const fishinghut: Art = (cx, cy, s, tier) => {
  const h = 12 * s;
  const ripple = at(cx + 2.5, cy + 2.5, 2);
  return {
    foot: 5,
    height: h + 12,
    node: (
      <g>
        <Box cx={cx} cy={cy} hx={5} hy={4} h={2} faces={WATER} />
        <ellipse cx={ripple.x} cy={ripple.y} rx={8} ry={3} fill="none" stroke="#e0f2fe" strokeWidth={1} />
        <ellipse cx={ripple.x + 6} cy={ripple.y + 3} rx={5} ry={2} fill="none" stroke="#e0f2fe" strokeWidth={1} />
        {/* Пірс на палях */}
        <Cylinder cx={cx + 0.5} cy={cy + 1.5} r={0.2} h={3} faces={WOOD} />
        <Cylinder cx={cx + 3.5} cy={cy + 1.5} r={0.2} h={3} faces={WOOD} />
        <Box cx={cx + 2} cy={cy + 1} hx={2.5} hy={0.6} h={1} z={2} faces={WOOD} />
        <Box cx={cx - 1.5} cy={cy - 1} hx={2} hy={2} h={h} z={2} faces={WOOD} />
        <Door cx={cx - 1.5} cy={cy - 1} hx={2} hy={2} z={2} side="left" h={7} />
        <Windows cx={cx - 1.5} cy={cy - 1} hx={2} hy={2} z={2} h={h} side="right" cols={1} />
        <Gable cx={cx - 1.5} cy={cy - 1} hx={2.4} hy={2.4} z={h + 2} h={9} faces={ROOF_BLUE} />
        <Tiles cx={cx - 1.5} cy={cy - 1} hx={2.4} hy={2.4} z={h + 2} h={9} kind="gable" />
        {/* Човен біля пірса */}
        <Box cx={cx + 4.2} cy={cy + 3} hx={1.3} hy={0.5} h={1} z={2} faces={ROOF_BROWN} />
        {tier >= 1 && <Cylinder cx={cx + 4.2} cy={cy + 3} r={0.12} z={3} h={9} faces={WOOD} />}
        <Barrel x={cx + 1} y={cy - 2.5} s={0.8} />
        {tier >= 2 && <Lantern x={cx + 4.5} y={cy + 0.5} z={3} h={8} />}
      </g>
    ),
  };
};

const beastpen: Art = (cx, cy, _s, tier) => ({
  foot: 4.5,
  height: 14,
  node: (
    <g>
      <Box cx={cx} cy={cy} hx={4.5} hy={4.5} h={1} faces={SOIL} />
      <Fence from={{ x: cx - 4.5, y: cy - 4.5 }} to={{ x: cx + 4.5, y: cy - 4.5 }} h={5} />
      <Fence from={{ x: cx - 4.5, y: cy - 4.5 }} to={{ x: cx - 4.5, y: cy + 4.5 }} h={5} />
      <Cylinder cx={cx - 1} cy={cy - 1} r={1.3} z={1} h={5} faces={HAY} />
      <Cylinder cx={cx + 1.5} cy={cy + 1} r={1.1} z={1} h={4} faces={HAY} />
      {/* Навіс */}
      <Cylinder cx={cx - 3.5} cy={cy + 2.5} r={0.2} z={1} h={7} faces={WOOD} />
      <Cylinder cx={cx - 1} cy={cy + 3.5} r={0.2} z={1} h={7} faces={WOOD} />
      <Gable cx={cx - 2.2} cy={cy + 3} hx={1.8} hy={1.2} z={8} h={4} faces={ROOF_BROWN} />
      <Fence from={{ x: cx + 4.5, y: cy - 4.5 }} to={{ x: cx + 4.5, y: cy + 4.5 }} h={5} />
      <Fence from={{ x: cx - 4.5, y: cy + 4.5 }} to={{ x: cx + 4.5, y: cy + 4.5 }} h={5} />
      <Box cx={cx + 3} cy={cy + 3} hx={1.2} hy={0.4} h={1.5} z={1} faces={WOOD} />
      {tier >= 1 && <Cylinder cx={cx + 3} cy={cy - 2.5} r={1} z={1} h={4} faces={HAY} />}
      {tier >= 2 && <Flag x={cx - 4.5} y={cy - 4.5} z={5} h={12} color={TIER_FLAG[2]} />}
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
        <Windows cx={cx} cy={cy} hx={3.5} hy={3.5} h={h} side="left" />
        <Door cx={cx} cy={cy} hx={3.5} hy={3.5} side="left" h={9} />
        <Gable cx={cx} cy={cy} hx={3.9} hy={3.9} z={h} h={12} faces={ROOF_BLUE} />
        <Tiles cx={cx} cy={cy} hx={3.9} hy={3.9} z={h} h={12} kind="gable" />
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
  ironmine: mine(ROCK_IRON, IRON),
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

/** Будівля трохи росте з рівнем, але не безмежно — інакше закриє сусідів. */
export function buildingScale(level: number): number {
  return 1 + Math.min(level, 30) * 0.015;
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
    return artFor(buildingKey)(cx, cy, s, tierOf(level));
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
