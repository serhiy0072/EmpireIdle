import { useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import BattleReportList from "../components/map/BattleReportList";
import CellDetails from "../components/map/CellDetails";
import MarchList from "../components/map/MarchList";
import SendMarchForm from "../components/map/SendMarchForm";
import WorldMap from "../components/map/WorldMap";
import { useSession } from "../hooks/useSession";
import type { MarchTargetType } from "../lib/apiTypes";
import { MAP_RADIUS, useMapArea, useMapCell } from "../lib/queries/map";
import { useMarches } from "../lib/queries/marches";
import { useVillage } from "../lib/queries/village";

/** Крок зсуву центру кнопками: пів вікна, щоб сусідні ділянки перекривались. */
const PAN_STEP = MAP_RADIUS;

type Target = { type: MarchTargetType; id: string; name: string };

export default function MapPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const village = useVillage(playerId);
  const marches = useMarches(playerId);

  // Зсув від дому, а не абсолютний центр: до приходу села координат ще немає
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const [selected, setSelected] = useState<{ x: number; y: number } | null>(null);
  const [target, setTarget] = useState<Target | null>(null);

  const home = village.data === undefined ? null : { x: village.data.x, y: village.data.y };
  const centerX = home === null ? null : home.x + offset.x;
  const centerY = home === null ? null : home.y + offset.y;

  const area = useMapArea(centerX, centerY);
  const cell = useMapCell(selected?.x ?? null, selected?.y ?? null);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження мапи…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const pan = (dx: number, dy: number) => setOffset((prev) => ({ x: prev.x + dx, y: prev.y + dy }));
  const isHome = selected !== null && home !== null && selected.x === home.x && selected.y === home.y;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Мапа</h1>
        <div className="flex items-center gap-1 text-sm">
          <button type="button" onClick={() => pan(-PAN_STEP, 0)} className="rounded-lg border border-slate-300 px-2 py-1 hover:bg-slate-100">
            ←
          </button>
          <button type="button" onClick={() => pan(0, -PAN_STEP)} className="rounded-lg border border-slate-300 px-2 py-1 hover:bg-slate-100">
            ↑
          </button>
          <button type="button" onClick={() => pan(0, PAN_STEP)} className="rounded-lg border border-slate-300 px-2 py-1 hover:bg-slate-100">
            ↓
          </button>
          <button type="button" onClick={() => pan(PAN_STEP, 0)} className="rounded-lg border border-slate-300 px-2 py-1 hover:bg-slate-100">
            →
          </button>
          <button
            type="button"
            onClick={() => setOffset({ x: 0, y: 0 })}
            disabled={offset.x === 0 && offset.y === 0}
            className="rounded-lg border border-slate-300 px-3 py-1 hover:bg-slate-100 disabled:opacity-50"
          >
            Додому
          </button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div>
          {area.isPending || home === null ? (
            <div className="aspect-square w-full animate-pulse rounded-xl bg-slate-200" />
          ) : area.isError ? (
            <ErrorBanner error={area.error} />
          ) : (
            <WorldMap
              area={area.data}
              home={home}
              marches={marches.data ?? []}
              selected={selected}
              onSelect={(x, y) => {
                setSelected({ x, y });
                setTarget(null);
              }}
            />
          )}
        </div>

        <div className="space-y-4">
          {target !== null ? (
            <SendMarchForm playerId={playerId} target={target} onSent={() => setTarget(null)} onCancel={() => setTarget(null)} />
          ) : selected === null ? (
            <p className="text-sm text-slate-500">Оберіть клітину на мапі: ☠ — монстр, ⌂ — чуже село, ★ — ваше.</p>
          ) : cell.isPending ? (
            <p className="text-sm text-slate-500">Дивимось…</p>
          ) : cell.isError ? (
            <ErrorBanner error={cell.error} />
          ) : (
            <CellDetails cell={cell.data} isHome={isHome} onAttack={setTarget} />
          )}

          <section className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Походи</h2>
            {marches.isError ? <ErrorBanner error={marches.error} /> : <MarchList playerId={playerId} marches={marches.data ?? []} />}
          </section>
        </div>
      </div>

      <section className="space-y-2" data-tutorial="reports">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Звіти боїв</h2>
        <BattleReportList playerId={playerId} />
      </section>
    </div>
  );
}
