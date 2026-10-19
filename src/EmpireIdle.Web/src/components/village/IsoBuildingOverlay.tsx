import { at } from "../../lib/iso";
import { ResourceGlyph } from "../shell/icons";
import type { ArtResult } from "./buildingArt";

interface Props {
  art: ArtResult;
  x: number;
  y: number;
  name: string;
  /** Рівень у значку; null — функціональна будівля без рівнів. */
  level: number | null;
  /** Рівень ратуші, що відкриває будівлю; null — уже відкрита. */
  gateLevel: number | null;
  /** Ресурс бульбашки збору — його знак поруч із числом. */
  resource: string | null;
  /** Текст бульбашки збору. null — збирати нічого. */
  bubble: string | null;
  /** Масштаб камери: значки тримають свій розмір на екрані, хоч світ віддаляється. */
  zoom: number;
  onSelect: () => void;
  onCollect: () => void;
}

/** Нижче цього масштабу назви ховаються — лишається значок рівня, інакше підписи налазять один на одного. */
const NAMES_FROM_ZOOM = 0.9;

/**
 * Табличка й бульбашка будівлі. Малюються після всіх силуетів:
 * інакше ближня будівля перекриває підпис дальньої.
 */
export default function IsoBuildingOverlay({ art, x, y, name, level, gateLevel, resource, bubble, zoom, onSelect, onCollect }: Props) {
  const anchor = at(x, y, art.height);
  const labelPoint = at(x + art.foot, y + art.foot);
  // Розмір на екрані сталий до віддалення: 1/zoom скасовує масштаб камери
  const scale = Math.max(1, 1 / zoom);
  const showName = zoom >= NAMES_FROM_ZOOM || gateLevel !== null;
  const text = gateLevel !== null ? `🔒 Ратуша ${gateLevel}` : name;
  const nameWidth = showName ? text.length * 6.4 + 14 : 0;
  const badge = level !== null && gateLevel === null;

  return (
    <g>
      <g className="cursor-pointer" transform={`translate(${labelPoint.x} ${labelPoint.y + 16}) scale(${scale})`} onClick={onSelect}>
        {showName && (
          <g>
            <rect
              x={badge ? -4 : -nameWidth / 2}
              y={-9}
              width={nameWidth}
              height={18}
              rx={5}
              fill="#16305e"
              stroke="#5b85c4"
              strokeWidth={1.2}
            />
            <text x={badge ? -4 + nameWidth / 2 : 0} textAnchor="middle" y={4} fontSize={11} fontWeight={700} fill="white">
              {text}
            </text>
          </g>
        )}
        {badge && (
          <g transform={showName ? "translate(-8 0)" : undefined}>
            <circle r={11} fill="#f59e0b" stroke="#78350f" strokeWidth={1.5} />
            <text textAnchor="middle" y={4.5} fontSize={12} fontWeight={800} fill="white" stroke="#78350f" strokeWidth={0.6}>
              {level}
            </text>
          </g>
        )}
      </g>

      {bubble !== null && (
        <g className="cursor-pointer" transform={`translate(${anchor.x} ${anchor.y - 22}) scale(${scale})`} onClick={onCollect}>
          <path d="M-6 12 L0 20 L6 12 Z" fill="white" stroke="#16a34a" strokeWidth={2} />
          <rect x={-36} y={-15} width={72} height={28} rx={14} fill="white" stroke="#16a34a" strokeWidth={2} />
          {resource !== null && (
            <g transform="translate(-32 -11)">
              <ResourceGlyph resource={resource} size={22} />
            </g>
          )}
          <text x={resource !== null ? 6 : 0} textAnchor="middle" y={5} fontSize={13} fontWeight={800} fill="#166534">
            {bubble}
          </text>
        </g>
      )}
    </g>
  );
}
