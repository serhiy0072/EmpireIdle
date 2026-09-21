import { at, toPath } from "../../lib/iso";
import type { ArtResult } from "./buildingArt";

interface Props {
  art: ArtResult;
  x: number;
  y: number;
  selected: boolean;
  underConstruction: boolean;
  /** Під туманом війни (GDD §3.1): силует видно, взаємодія — лише щоб побачити умову відкриття. */
  locked: boolean;
  onSelect: () => void;
}

/** Силует будівлі. Підпис і бульбашка — в окремому шарі поверх усієї мапи. */
export default function IsoBuilding({ art, x, y, selected, underConstruction, locked, onSelect }: Props) {
  const f = art.foot + 0.8;
  const ground = [at(x - f, y - f), at(x + f, y - f), at(x + f, y + f), at(x - f, y + f)];

  return (
    <g className="cursor-pointer" onClick={onSelect}>
      {(selected || underConstruction) && (
        <path
          d={toPath(ground)}
          fill={selected ? "rgba(16, 185, 129, 0.25)" : "none"}
          stroke={selected ? "#10b981" : "#f59e0b"}
          strokeWidth={3}
          strokeDasharray={underConstruction ? "10 6" : undefined}
        />
      )}

      <g opacity={locked ? 0.45 : underConstruction ? 0.55 : 1} style={locked ? { filter: "grayscale(1)" } : undefined}>
        {/* М'яка тінь під основою: і спрайт, і силует стоять на землі, а не висять */}
        <path d={toPath(ground)} fill="rgba(15, 23, 42, 0.12)" />
        {art.node}
      </g>
    </g>
  );
}
