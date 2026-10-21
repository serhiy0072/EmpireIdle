import { useCallback, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton } from "../components/game/GameUi";
import BattleReportList from "../components/map/BattleReportList";
import CellDetails from "../components/map/CellDetails";
import type { MapMarch } from "../components/map/MarchAnimation";
import MarchList from "../components/map/MarchList";
import SendMarchForm from "../components/map/SendMarchForm";
import WorldMap from "../components/map/WorldMap";
import { useSession } from "../hooks/useSession";
import type { MarchIntent, MarchTargetType } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useMyClan } from "../lib/queries/clans";
import { useUseItem } from "../lib/queries/inventory";
import { useMapArea, useMapCell, type MapView } from "../lib/queries/map";
import { MARCH_INTENT, MARCH_STATE, useIncomingAttacks, useMarches } from "../lib/queries/marches";
import { useClanTerritory } from "../lib/queries/territory";
import { useVillage } from "../lib/queries/village";
import { useHotkeys } from "../hooks/useHotkeys";

type Target = { type: MarchTargetType; id: string; name: string; intent?: MarchIntent };

export default function MapPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const catalog = useCatalog();
  const village = useVillage(playerId);
  const marches = useMarches(playerId);
  const incoming = useIncomingAttacks(playerId);
  const territory = useClanTerritory(playerId);
  const myClan = useMyClan(playerId);

  // Телепорт: інвентар приводить сюди з ?teleport=<ключ>, місце обирають кліком, дію можна скасувати
  const [searchParams, setSearchParams] = useSearchParams();
  const teleportKey = searchParams.get("teleport");
  const teleportItem = teleportKey === null ? null : catalog.item(teleportKey);
  // Межа типу телепорта — щоб гравець не клікав навмання, а сервер не відмовляв (GDD §8.9)
  const teleportHint =
    teleportItem?.teleportScope === "Nearby"
      ? `Цей телепорт дістає не далі ${teleportItem.teleportRange ?? 0} клітин від поселення.`
      : teleportItem?.teleportScope === "ClanTerritory"
        ? "Лише на території вашого клану — поруч зі спорудою."
        : null;
  const teleport = useUseItem(playerId);
  const cancelTeleport = () => setSearchParams({});

  // Ділянка задається камерою; до приходу села її немає, і мапа починає з дому
  const [view, setView] = useState<MapView | null>(null);
  const [selected, setSelected] = useState<{ x: number; y: number } | null>(null);
  const [target, setTarget] = useState<Target | null>(null);
  const [homeRequest, setHomeRequest] = useState(0);
  const [reports, setReports] = useState(false);
  const [focusRequest, setFocusRequest] = useState<{ x: number; y: number; n: number } | null>(null);

  // Тривога веде сюди з ?focus=x,y,<марш>: ціль у кадр і виділена. Ключ із маршем —
  // щоб та сама клітина з новою тривогою знову наводила камеру. Робимо під час рендеру,
  // а не в ефекті: це похідний від URL стан, і зайвого кадру без виділення не буде
  const focusParam = searchParams.get("focus");
  const [handledFocus, setHandledFocus] = useState<string | null>(null);

  // Esc спершу закриває вікно звітів, далі — дії вибраної клітини, і лише потім веде назад
  useHotkeys({ Escape: () => setReports(false) }, { modal: true, enabled: reports });
  useHotkeys({ Escape: () => setSelected(null) }, { enabled: selected !== null });

  if (focusParam !== null && focusParam !== handledFocus) {
    const [fx, fy] = focusParam.split(",").map(Number);
    setHandledFocus(focusParam);

    if (fx !== undefined && fy !== undefined && Number.isInteger(fx) && Number.isInteger(fy)) {
      setSelected({ x: fx, y: fy });
      setTarget(null);
      setView({ x: fx, y: fy, radius: 12 });
      setFocusRequest((previous) => ({ x: fx, y: fy, n: (previous?.n ?? 0) + 1 }));
    }
  }

  // Стабільний об'єкт: мапа перераховує кадр лише коли село справді переїхало
  const homeX = village.data?.x;
  const homeY = village.data?.y;
  const home = useMemo(() => (homeX === undefined || homeY === undefined ? null : { x: homeX, y: homeY }), [homeX, homeY]);
  const effectiveView = view ?? (home === null ? null : { x: home.x, y: home.y, radius: 12 });
  const area = useMapArea(effectiveView);

  // Камера показує інше — мапа просить ділянку під нею
  const changeView = useCallback((next: MapView) => setView(next), []);
  const cell = useMapCell(selected?.x ?? null, selected?.y ?? null);

  // Свої марші йдуть від дому, повернення — назад до нього; обидві ноги ведемо від legStartedAt.
  // Ворожі — від поселення нападника
  const mapMarches = useMemo<MapMarch[]>(() => {
    if (home === null) return [];

    // Табір стоїть на місці — його не ведемо, а малюємо маркером
    const own = (marches.data ?? []).filter((march) => march.state !== MARCH_STATE.camping).map((march) =>
      march.state === MARCH_STATE.returning
        ? { id: march.id, fromX: march.targetX, fromY: march.targetY, toX: home.x, toY: home.y,
            departedAt: march.legStartedAt, arrivesAt: march.arrivesAt, hostile: false,
            scout: march.intent === MARCH_INTENT.scout }
        : { id: march.id, fromX: home.x, fromY: home.y, toX: march.targetX, toY: march.targetY,
            departedAt: march.legStartedAt, arrivesAt: march.arrivesAt, hostile: false,
            scout: march.intent === MARCH_INTENT.scout },
    );
    const hostile = (incoming.data ?? []).map((attack) => ({
      id: attack.marchId, fromX: attack.fromX, fromY: attack.fromY, toX: attack.targetX, toY: attack.targetY,
      departedAt: attack.departedAt, arrivesAt: attack.arrivesAt, hostile: true,
      scout: attack.intent === MARCH_INTENT.scout,
    }));

    return [...own, ...hostile];
  }, [home, marches.data, incoming.data]);

  if (village.isPending) {
    return <p className="text-slate-500">Завантаження мапи…</p>;
  }

  if (village.isError) {
    return <ErrorBanner error={village.error} />;
  }

  const isHome = selected !== null && home !== null && selected.x === home.x && selected.y === home.y;

  // Ближній телепорт: відстань Чебишева, як рахує сервер — кажемо одразу, а не після відмови
  const teleportRange = teleportItem?.teleportScope === "Nearby" ? teleportItem.teleportRange : null;
  const tooFar =
    teleportRange != null &&
    selected !== null &&
    home !== null &&
    Math.max(Math.abs(selected.x - home.x), Math.abs(selected.y - home.y)) > teleportRange;

  const panelOpen = selected !== null;

  // Вікно дій над обраною клітиною: його ставить і веде за клітиною сама мапа
  const cellPanel = !panelOpen ? null : (
    <>
      <button
        type="button"
        aria-label="Закрити"
        onClick={() => {
          setSelected(null);
          setTarget(null);
        }}
        className="absolute right-2 top-1.5 z-10 text-lg font-bold text-slate-500 hover:text-slate-800"
      >
        ✕
      </button>
      {cell.isPending ? (
        <p className="p-4 text-sm text-slate-500">Дивимось…</p>
      ) : cell.isError ? (
        <div className="p-4">
          <ErrorBanner error={cell.error} />
        </div>
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
          ) : tooFar ? (
            <p className="text-sm text-amber-800">
              Задалеко: цей телепорт дістає не далі {teleportItem?.teleportRange} клітин від поселення.
            </p>
          ) : (
            <p className="text-sm text-slate-600">
              Село переїде разом із гарнізоном; усі ваші війська — з походів, таборів і підкріплень у союзників —
              одразу будуть удома.
            </p>
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
              disabled={teleport.isPending || !cell.data.habitable || cell.data.occupantType != null || tooFar}
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
        <CellDetails
          // Своя кнопка розвідки на кожну клітину: стан «в дорозі» не має переїжджати на сусідню
          key={`${cell.data.x}:${cell.data.y}`}
          playerId={playerId}
          cell={cell.data}
          isHome={isHome}
          territory={territory.data}
          threats={(incoming.data ?? []).filter((a) => a.targetX === cell.data.x && a.targetY === cell.data.y)}
          camps={(area.data?.camps ?? []).filter((c) => c.x === cell.data.x && c.y === cell.data.y)}
          onMarch={setTarget}
        />
      )}
    </>
  );

  return (
    // Мапа — на весь екран; панелі лежать поверх поля, як HUD гри
    <div className="relative h-full">
      {area.data === undefined || home === null || effectiveView === null ? (
        area.isError ? (
          <div className="p-4">
            <ErrorBanner error={area.error} />
          </div>
        ) : (
          <div className="h-full w-full animate-pulse bg-[#bfd9a0]" />
        )
      ) : (
        <WorldMap
          area={area.data}
          home={home}
          view={effectiveView}
          marches={mapMarches}
          selected={selected}
          homeRequest={homeRequest}
          focusRequest={focusRequest}
          mapSize={catalog.mapSize}
          ownClanId={myClan.data?.id ?? null}
          coverageRadius={territory.data?.enabled === true ? territory.data.radius : 0}
          playerId={playerId}
          onSelect={(x, y) => {
            setSelected({ x, y });
            setTarget(null);
          }}
          popup={cellPanel}
          onViewChange={changeView}
        />
      )}

      <div className="pointer-events-none absolute inset-x-3 top-3 flex flex-wrap items-start gap-2">
        <h1 className="rounded-xl bg-gradient-to-b from-[#24467f] to-[#16305e] px-4 py-1.5 text-base font-semibold text-white shadow-lg ring-2 ring-[#5b85c4] [text-shadow:0_1px_1px_#0008]">
          Мапа світу
          {home !== null && <span className="ml-2 text-xs font-normal text-sky-200">ваше село ({home.x}, {home.y})</span>}
        </h1>
        <div className="flex-1" />
        <div className="pointer-events-auto flex gap-2">
          <span data-tutorial="reports">
            <GameButton onClick={() => setReports(true)} variant="secondary">
              Звіти
            </GameButton>
          </span>
          <GameButton
            onClick={() => {
              setView(null);
              setHomeRequest((value) => value + 1);
            }}
          >
            Додому
          </GameButton>
        </div>
      </div>

      <div className="absolute left-3 top-16 w-[min(28rem,calc(100%-1.5rem))] space-y-2">
        {teleportKey !== null && (
          <div role="status" className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-emerald-300 bg-emerald-50/95 px-4 py-3 shadow-lg">
            <span className="text-sm text-emerald-900">
              Переселення: оберіть вільну придатну клітину на мапі й підтвердіть у вікні біля неї.
              {teleportHint !== null && ` ${teleportHint}`}
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
      </div>

      {/* Права панель: походи; на вузькому екрані — знизу */}
      <aside className="pointer-events-none absolute inset-x-3 bottom-3 flex max-h-[60%] flex-col gap-2 sm:inset-x-auto sm:right-3 sm:top-16 sm:max-h-none sm:w-80">
        {(marches.data?.length ?? 0) > 0 || marches.isError ? (
          <section className="pointer-events-auto space-y-2 rounded-xl bg-white/95 p-3 shadow-2xl ring-2 ring-[#5b85c4]">
            <h2 className="text-sm font-semibold text-slate-800">Походи</h2>
            {marches.isError ? <ErrorBanner error={marches.error} /> : <MarchList playerId={playerId} marches={marches.data ?? []} />}
          </section>
        ) : (
          !panelOpen && (
            <p className="rounded-xl bg-[#1d3b6f]/90 px-3 py-2 text-xs text-sky-100 shadow-lg ring-1 ring-[#5b85c4]">
              Торкніться клітини: монстр, село, вежа клану чи намет табору — і тут з’являться дії.
            </p>
          )
        )}
      </aside>

      {target !== null && (
        <SendMarchForm playerId={playerId} target={target} onSent={() => setTarget(null)} onCancel={() => setTarget(null)} />
      )}

      {reports && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Звіти"
          onClick={() => setReports(false)}
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
        >
          <div
            onClick={(event) => event.stopPropagation()}
            className="flex max-h-[85vh] w-full max-w-2xl flex-col overflow-hidden rounded-2xl bg-sky-50 text-slate-800 shadow-2xl ring-2 ring-sky-300"
          >
            <div className="flex items-center gap-2 bg-gradient-to-b from-sky-500 to-sky-600 px-4 py-2 text-white">
              <h2 className="flex-1 text-center text-lg font-semibold">Звіти</h2>
              <button type="button" aria-label="Закрити" onClick={() => setReports(false)} className="text-xl font-bold hover:text-sky-100">
                ✕
              </button>
            </div>
            <div className="min-h-0 flex-1 space-y-4 overflow-y-auto p-4">
              {/* Звіти розвідки лежать у скриньці — тут лише бої */}
              <section className="space-y-2">
                <h3 className="text-sm font-semibold">Бої</h3>
                <BattleReportList playerId={playerId} />
              </section>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
