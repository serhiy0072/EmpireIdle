import { at } from "../../lib/iso";
import type { ArtResult } from "./buildingArt";

interface Props {
  art: ArtResult;
  x: number;
  y: number;
  name: string;
  /** Рівень на табличці; null — функціональна будівля без рівнів. */
  level: number | null;
  /** Рівень ратуші, що відкриває будівлю; null — уже відкрита. */
  gateLevel: number | null;
  /** Масштаб камери: значки тримають свій розмір на екрані, хоч світ віддаляється. */
  zoom: number;
  /** Показати табличку з назвою й рівнем: камера рухається або на будівлю навели. */
  labelled: boolean;
  onSelect: () => void;
}

/**
 * Табличка «назва · рівень» будівлі. Малюються після всіх силуетів:
 * інакше ближня будівля перекриває підпис дальньої. Табличка — лише коли labelled
 * (камера рухається чи будівлю навели); замок закритої будівлі видно завжди.
 */
export default function IsoBuildingOverlay({ art, x, y, name, level, gateLevel, zoom, labelled, onSelect }: Props) {
  // Табличка над дахом, а не під фундаментом: так видно, якій будівлі вона належить
  const plate = at(x, y, art.height + 18);
  // Розмір на екрані сталий до віддалення: 1/zoom скасовує масштаб камери
  const scale = Math.max(1, 1 / zoom);
  const locked = gateLevel !== null;
  const text = locked ? `🔒 Ратуша ${gateLevel}` : name;
  const textWidth = text.length * 6.6;
  const chip = level !== null && !locked;
  const chipWidth = chip ? String(level).length * 8 + 14 : 0;
  const width = textWidth + 16 + (chip ? chipWidth + 6 : 0);

  return (
    <g>
      {(labelled || locked) && (
        <g className="cursor-pointer" transform={`translate(${plate.x} ${plate.y}) scale(${scale})`} onClick={onSelect}>
          <path d="M-5 11 L0 17 L5 11 Z" fill="#16305e" />
          <rect x={-width / 2} y={-11} width={width} height={22} rx={7} fill="#16305e" fillOpacity={0.92} stroke="#8fb4ea" strokeWidth={1.2} />
          <text x={-width / 2 + 8} y={4.5} fontSize={12} fontWeight={700} fill="white">
            {text}
          </text>
          {chip && (
            <g transform={`translate(${width / 2 - chipWidth / 2 - 4} 0)`}>
              <rect x={-chipWidth / 2} y={-8} width={chipWidth} height={16} rx={8} fill="#fde68a" />
              <text textAnchor="middle" y={4.5} fontSize={12} fontWeight={800} fill="#78350f">
                {level}
              </text>
            </g>
          )}
        </g>
      )}
    </g>
  );
}
