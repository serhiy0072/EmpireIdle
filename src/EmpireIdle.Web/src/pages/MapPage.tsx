import { useCallback, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import BattleReportList from "../components/map/BattleReportList";
import CellDetails from "../components/map/CellDetails";
import MarchList from "../components/map/MarchList";
import SendMarchForm from "../components/map/SendMarchForm";
import WorldMap from "../components/map/WorldMap";
import { useSession } from "../hooks/useSession";
import type { MarchTargetType } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useUseItem } from "../lib/queries/inventory";
import { useMapArea, useMapCell, type MapView } from "../lib/queries/map";
import { useMarches } from "../lib/queries/marches";
import { useVillage } from "../lib/queries/village";

type Target = { type: MarchTargetType; id: string; name: string };

export default function MapPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const catalog = useCatalog();
  const village = useVillage(playerId);
  const marches = useMarches(playerId);

  // Телепорт: інвентар приводить сюди з ?teleport=<ключ>, місце обирають кліком, дію можна скасувати
  const [searchParams, setSearchParams] = useSearchParams();
  const teleportKey = searchParams.get("teleport");
  const teleport = useUseItem(playerId);
  const cancelTeleport = () => setSearchParams({});

  // Ділянка задається камерою; до приходу села її немає, і мапа починає з дому
  const [view, setView] = useState<MapView | null>(null);
  const [selected, setSelected] = useState<{ x: number; y: number } | null>(null);
  const [target, setTarget] = useState<Target | null>(null);
  const [homeRequest, setHomeRequest] = useState(0);

  // Стабільний об'єкт: мапа перераховує кадр лише коли село справді переїхало
  const homeX = village.data?.x;
  const homeY = village.data?.y;
  const home = useMemo(() => (homeX === undefined || homeY === undefined ? null : { x: homeX, y: homeY }), [homeX, homeY]);
  const effectiveView = view ?? (home === null ? null : { x: home.x, y: home.y, radius: 12 });
  const area = useMapArea(effectiveView);

  // Камера показує інше — мапа просить ділянку під нею
  const changeView = useCallback((next: MapView) => setView(next), []);
  const cell = useMapCell(selected?.x ?? null, selected?.y ?? null);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження мапи…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const isHome = selected !== null && home !== null && selected.x === home.x && selected.y === home.y;

  return (
    <div className="space-y-4">
      {teleportKey !== null && (
        <div role="status" className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-emerald-300 bg-emerald-50 px-4 py-3">
          <span className="text-sm text-emerald-900">
            Переселення: оберіть вільну придатну клітину на мапі й підтвердіть у панелі праворуч.
          </span>
          <button
            type="button"
            onClick={cancelTeleport}
            className="rounded-lg border border-emerald-300 px-3 py-1 text-sm text-emerald-900 hover:bg-emerald-100"
          >
            Скасувати
          </button>
        </div>
      )}

      <ErrorBanner error={teleport.error} />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Мапа</h1>
        <div className="flex items-center gap-2 text-sm">
          {home !== null && (
            <span className="text-xs text-slate-500">
              ({home.x}, {home.y}) · тягніть, щоб рухатись, колесо — масштаб
            </span>
          )}
          <button
            type="button"
            onClick={() => {
              setView(null);
              setHomeRequest((value) => value + 1);
            }}
            className="rounded-lg border border-slate-300 px-3 py-1 hover:bg-slate-100"
          >
            Додому
          </button>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div>
          {area.data === undefined || home === null || effectiveView === null ? (
            area.isError ? (
              <ErrorBanner error={area.error} />
            ) : (
              <div className="aspect-square w-full animate-pulse rounded-xl bg-slate-200 lg:aspect-auto lg:h-[640px]" />
            )
          ) : (
            <WorldMap
              area={area.data}
              home={home}
              view={effectiveView}
              marches={marches.data ?? []}
              selected={selected}
              homeRequest={homeRequest}
              mapSize={catalog.mapSize}
              onSelect={(x, y) => {
                setSelected({ x, y });
                setTarget(null);
              }}
              onViewChange={changeView}
            />
          )}
        </div>

        <div className="space-y-4">
          {target !== null ? (
            <SendMarchForm playerId={playerId} target={target} onSent={() => setTarget(null)} onCancel={() => setTarget(null)} />
          ) : selected === null ? (
            <p className="text-sm text-slate-500">
              Оберіть клітину на мапі: рогата істота — монстр, синій дах — чуже село, червоний з прапором — ваше.
            </p>
          ) : cell.isPending ? (
            <p className="text-sm text-slate-500">Дивимось…</p>
          ) : cell.isError ? (
            <ErrorBanner error={cell.error} />
          ) : teleportKey !== null ? (
            <div className="space-y-3 rounded-xl border border-emerald-300 bg-white p-4">
              <div className="flex items-baseline justify-between gap-2">
                <h3 className="font-medium text-slate-800">Переселитися сюди?</h3>
                <span className="text-xs text-slate-500">
                  ({cell.data.x}, {cell.data.y})
                </span>
              </div>
              {/* Придатність вирішує сервер, але очевидне кажемо одразу: вода й зайняте не підходять */}
              {!cell.data.habitable ? (
                <p className="text-sm text-amber-800">Тут не оселитись — оберіть рівнину, ліс або гори.</p>
              ) : cell.data.occupantType != null ? (
                <p className="text-sm text-amber-800">Клітина зайнята — оберіть вільну.</p>
              ) : (
                <p className="text-sm text-slate-600">Село переїде разом із гарнізоном; армії в дорозі розвернуться додому.</p>
              )}
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() =>
                    teleport.mutate(
                      { itemKey: teleportKey, count: 1, targetX: cell.data.x, targetY: cell.data.y },
                      { onSuccess: () => {
                        cancelTeleport();
                        setView(null);
                        setHomeRequest((value) => value + 1);
                      } },
                    )
                  }
                  disabled={teleport.isPending || !cell.data.habitable || cell.data.occupantType != null}
                  className="flex-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
                >
                  Переселитися
                </button>
                <button type="button" onClick={cancelTeleport} className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50">
                  Скасувати
                </button>
              </div>
            </div>
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
