import type { ReactElement } from "react";
import { at, toPath, UNIT_X, UNIT_Y, type Faces } from "../../lib/iso";

interface BoxProps {
  cx: number;
  cy: number;
  hx: number;
  hy: number;
  h: number;
  z?: number;
  faces: Faces;
  stroke?: string;
}

export function Box({ cx, cy, hx, hy, h, z = 0, faces: f, stroke }: BoxProps): ReactElement {
  return (
    <g>
      <path d={toPath([at(cx - hx, cy + hy, z), at(cx + hx, cy + hy, z), at(cx + hx, cy + hy, z + h), at(cx - hx, cy + hy, z + h)])} fill={f.left} />
      <path d={toPath([at(cx + hx, cy + hy, z), at(cx + hx, cy - hy, z), at(cx + hx, cy - hy, z + h), at(cx + hx, cy + hy, z + h)])} fill={f.right} />
      <path
        d={toPath([at(cx - hx, cy - hy, z + h), at(cx + hx, cy - hy, z + h), at(cx + hx, cy + hy, z + h), at(cx - hx, cy + hy, z + h)])}
        fill={f.top}
        stroke={stroke}
        strokeWidth={stroke === undefined ? undefined : 3}
      />
    </g>
  );
}

interface RoofProps {
  cx: number;
  cy: number;
  hx: number;
  hy: number;
  z: number;
  h: number;
  faces: Faces;
}

/** Чотирисхилий дах або насип: задні схили видно згори, тож малюються теж. */
export function Pyramid({ cx, cy, hx, hy, z, h, faces: f }: RoofProps): ReactElement {
  const apex = at(cx, cy, z + h);

  return (
    <g>
      <path d={toPath([at(cx - hx, cy - hy, z), at(cx + hx, cy - hy, z), apex])} fill={f.top} />
      <path d={toPath([at(cx - hx, cy + hy, z), at(cx - hx, cy - hy, z), apex])} fill={f.top} />
      <path d={toPath([at(cx - hx, cy + hy, z), at(cx + hx, cy + hy, z), apex])} fill={f.left} />
      <path d={toPath([at(cx + hx, cy + hy, z), at(cx + hx, cy - hy, z), apex])} fill={f.right} />
    </g>
  );
}

/** Двосхилий дах із гребенем уздовж осі X плану. */
export function Gable({ cx, cy, hx, hy, z, h, faces: f }: RoofProps): ReactElement {
  const start = at(cx - hx, cy, z + h);
  const end = at(cx + hx, cy, z + h);

  return (
    <g>
      <path d={toPath([at(cx - hx, cy - hy, z), at(cx + hx, cy - hy, z), end, start])} fill={f.top} />
      <path d={toPath([at(cx - hx, cy + hy, z), at(cx + hx, cy + hy, z), end, start])} fill={f.left} />
      <path d={toPath([at(cx + hx, cy - hy, z), at(cx + hx, cy + hy, z), end])} fill={f.right} />
    </g>
  );
}

interface RoundProps {
  cx: number;
  cy: number;
  r: number;
  z?: number;
  h: number;
  faces: Faces;
}

/** Коло плану радіуса r після проєкції стає еліпсом із цими півосями. */
function radii(r: number) {
  return { rx: r * UNIT_X * Math.SQRT2, ry: r * UNIT_Y * Math.SQRT2 };
}

export function Cylinder({ cx, cy, r, z = 0, h, faces: f }: RoundProps): ReactElement {
  const base = at(cx, cy, z);
  const top = at(cx, cy, z + h);
  const { rx, ry } = radii(r);

  return (
    <g>
      <ellipse cx={base.x} cy={base.y} rx={rx} ry={ry} fill={f.right} />
      <rect x={base.x - rx} y={top.y} width={rx * 2} height={base.y - top.y} fill={f.left} />
      <rect x={base.x} y={top.y} width={rx} height={base.y - top.y} fill={f.right} />
      <ellipse cx={top.x} cy={top.y} rx={rx} ry={ry} fill={f.top} />
    </g>
  );
}

export function Cone({ cx, cy, r, z = 0, h, faces: f }: RoundProps): ReactElement {
  const base = at(cx, cy, z);
  const apex = at(cx, cy, z + h);
  const { rx, ry } = radii(r);

  return (
    <g>
      <ellipse cx={base.x} cy={base.y} rx={rx} ry={ry} fill={f.right} />
      <path d={`M${base.x - rx} ${base.y} L${apex.x} ${apex.y} L${base.x} ${base.y + ry} Z`} fill={f.left} />
      <path d={`M${base.x} ${base.y + ry} L${apex.x} ${apex.y} L${base.x + rx} ${base.y} Z`} fill={f.right} />
    </g>
  );
}

interface FlagProps {
  x: number;
  y: number;
  z: number;
  h: number;
  color: string;
}

export function Flag({ x, y, z, h, color }: FlagProps): ReactElement {
  const foot = at(x, y, z);
  const top = at(x, y, z + h);

  return (
    <g>
      <line x1={foot.x} y1={foot.y} x2={top.x} y2={top.y} stroke="#334155" strokeWidth={2} />
      <path d={`M${top.x} ${top.y} L${top.x + 16} ${top.y + 5} L${top.x} ${top.y + 10} Z`} fill={color} />
    </g>
  );
}
