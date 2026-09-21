import { at, Box, Cone, Cylinder, faces } from "./isoShapes";
import { toPath } from "../../lib/iso";

/** Відступ стіни від краю ділянки й половина товщини — в одиницях плану. */
const INSET = 5;
const HALF = 0.9;
const FAR = INSET;
const NEAR = 100 - INSET;

/** Проріз брами на передньому лівому відрізку. */
const GATE_FROM = 44;
const GATE_TO = 56;

const WALL = faces("#e2e8f0", "#94a3b8", "#64748b");
const ROOF = faces("#fca5a5", "#dc2626", "#991b1b");

function wallHeight(level: number): number {
  return 14 + Math.min(level, 30) * 0.8;
}

function Tower({ x, y, h }: { x: number; y: number; h: number }) {
  return (
    <g>
      <Cylinder cx={x} cy={y} r={1.8} h={h + 12} faces={WALL} />
      <Cone cx={x} cy={y} r={2.3} z={h + 12} h={12} faces={ROOF} />
    </g>
  );
}

/** Зубці вздовж відрізка: кожні 3.5 одиниці плану. */
function Merlons({ from, to, fixed, alongX, h }: { from: number; to: number; fixed: number; alongX: boolean; h: number }) {
  const steps: number[] = [];
  for (let t = from + 1.5; t < to - 1; t += 3.5) steps.push(t);

  return (
    <>
      {steps.map((t) => (
        <Box
          key={t}
          cx={alongX ? t : fixed}
          cy={alongX ? fixed : t}
          hx={alongX ? 0.7 : HALF}
          hy={alongX ? HALF : 0.7}
          h={4}
          z={h}
          faces={WALL}
        />
      ))}
    </>
  );
}

interface Props {
  /** Задня частина малюється до будівель, передня — після, інакше стіна проходила б крізь будинки. */
  part: "back" | "front";
  level: number;
  name: string;
  selected: boolean;
  onSelect: () => void;
}

export default function IsoWall({ part, level, name, selected, onSelect }: Props) {
  const h = wallHeight(level);
  const stroke = selected ? "#10b981" : undefined;

  if (part === "back") {
    return (
      <g className="cursor-pointer" onClick={onSelect}>
        <Box cx={50} cy={FAR} hx={NEAR - 50} hy={HALF} h={h} faces={WALL} stroke={stroke} />
        <Box cx={FAR} cy={50} hx={HALF} hy={NEAR - 50} h={h} faces={WALL} stroke={stroke} />
        <Merlons from={FAR} to={NEAR} fixed={FAR} alongX h={h} />
        <Merlons from={FAR} to={NEAR} fixed={FAR} alongX={false} h={h} />
        <Tower x={FAR} y={FAR} h={h} />
      </g>
    );
  }

  const label = at(50, NEAR, 0);
  const text = `${name} · ${level}`;
  const width = text.length * 7 + 20;
  const gateLeft = at(GATE_FROM, NEAR + HALF, 0);
  const gateRight = at(GATE_TO, NEAR + HALF, 0);

  return (
    <g className="cursor-pointer" onClick={onSelect}>
      <Box cx={NEAR} cy={50} hx={HALF} hy={NEAR - 50} h={h} faces={WALL} stroke={stroke} />
      <Box cx={(FAR + GATE_FROM) / 2} cy={NEAR} hx={(GATE_FROM - FAR) / 2} hy={HALF} h={h} faces={WALL} stroke={stroke} />
      <Box cx={(GATE_TO + NEAR) / 2} cy={NEAR} hx={(NEAR - GATE_TO) / 2} hy={HALF} h={h} faces={WALL} stroke={stroke} />

      {/* Брама: темний проріз між двома вежами */}
      <path
        d={toPath([gateLeft, gateRight, at(GATE_TO, NEAR + HALF, h * 0.8), at(GATE_FROM, NEAR + HALF, h * 0.8)])}
        fill="#1e293b"
        opacity={0.35}
      />

      <Merlons from={NEAR * 0 + FAR} to={NEAR} fixed={NEAR} alongX={false} h={h} />
      <Merlons from={FAR} to={GATE_FROM} fixed={NEAR} alongX h={h} />
      <Merlons from={GATE_TO} to={NEAR} fixed={NEAR} alongX h={h} />

      <Tower x={NEAR} y={FAR} h={h} />
      <Tower x={FAR} y={NEAR} h={h} />
      <Tower x={GATE_FROM} y={NEAR} h={h} />
      <Tower x={GATE_TO} y={NEAR} h={h} />
      <Tower x={NEAR} y={NEAR} h={h} />

      <g transform={`translate(${label.x} ${label.y + 34})`}>
        <rect x={-width / 2} y={-11} width={width} height={20} rx={10} fill="rgba(15, 23, 42, 0.75)" />
        <text textAnchor="middle" y={4} fontSize={12} fontWeight={600} fill="white">
          {text}
        </text>
      </g>
    </g>
  );
}
