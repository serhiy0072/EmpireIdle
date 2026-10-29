import { at } from "../../lib/iso";
import type { ArtResult } from "./buildingArt";

export interface RingAction {
  key: string;
  icon: string;
  label: string;
  /** Підпис під назвою — ціна прискорення тощо. */
  note?: string;
  disabled?: boolean;
  /** Головна дія (прискорення) — тепліша кнопка. */
  accent?: boolean;
  tutorial?: string;
  onClick: () => void;
}

interface Props {
  art: ArtResult;
  x: number;
  y: number;
  zoom: number;
  actions: readonly RingAction[];
}

/** Радіус дуги на екрані й розмір кнопки — сталі до масштабу камери. */
const RADIUS = 92;
const BUTTON = 26;

/**
 * Кільце дій під вибраною будівлею, як на референсі 29.10.2026: «Подробиці», «Покращити»,
 * переходи на екрани будівлі, прискорення. Кнопки — дугою під силуетом, щоб не закривати дах і табличку.
 */
export default function BuildingActionRing({ art, x, y, zoom, actions }: Props) {
  const center = at(x, y, art.height * 0.35);
  const scale = Math.max(1, 1 / zoom);
  // Нижня дуга: від лівого краю до правого; одна кнопка — рівно під центром
  const spread = Math.min(150, 40 * (actions.length - 1));
  const first = 90 + spread / 2;
  const step = actions.length > 1 ? spread / (actions.length - 1) : 0;

  return (
    <g transform={`translate(${center.x} ${center.y}) scale(${scale})`}>
      {actions.map((action, index) => {
        const angle = ((first - index * step) * Math.PI) / 180;
        const bx = Math.cos(angle) * RADIUS;
        const by = Math.sin(angle) * RADIUS * 0.75;

        return (
          <g
            key={action.key}
            role="button"
            aria-label={action.label}
            aria-disabled={action.disabled}
            data-tutorial={action.tutorial}
            transform={`translate(${bx} ${by})`}
            className={action.disabled === true ? "cursor-not-allowed opacity-50" : "cursor-pointer"}
            onPointerDown={(event) => event.stopPropagation()}
            onClick={(event) => {
              event.stopPropagation();
              if (action.disabled !== true) action.onClick();
            }}
          >
            <polygon
              points={hexagon(BUTTON)}
              fill={action.accent === true ? "#f59e0b" : "#3b6fb6"}
              stroke="#dbeafe"
              strokeWidth={2.5}
              className="drop-shadow"
            />
            <text textAnchor="middle" y={8} fontSize={22}>
              {action.icon}
            </text>
            <text
              textAnchor="middle"
              y={BUTTON + 15}
              fontSize={12}
              fontWeight={700}
              fill="white"
              stroke="#16305e"
              strokeWidth={3}
              paintOrder="stroke"
            >
              {action.label}
            </text>
            {action.note !== undefined && (
              <text
                textAnchor="middle"
                y={BUTTON + 29}
                fontSize={11}
                fontWeight={700}
                fill="#bae6fd"
                stroke="#16305e"
                strokeWidth={3}
                paintOrder="stroke"
              >
                {action.note}
              </text>
            )}
          </g>
        );
      })}
    </g>
  );
}

/** Шестикутник «на вершині», як кнопки на референсі. */
function hexagon(radius: number): string {
  return Array.from({ length: 6 }, (_, i) => {
    const angle = ((60 * i - 90) * Math.PI) / 180;
    return `${Math.cos(angle) * radius},${Math.sin(angle) * radius}`;
  }).join(" ");
}
