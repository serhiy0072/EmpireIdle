import { useNow } from "../../hooks/useNow";
import type {
  ClanTerritoryResponse,
  IncomingAttackResponse,
  MapCampResponse,
  MapCellDetailsResponse,
  MarchIntent,
  MarchTargetType,
} from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { MARCH_INTENT, MARCH_TARGET, useRecallCamp } from "../../lib/queries/marches";
import { useSendScout } from "../../lib/queries/scouting";
import { usePlaceStructure } from "../../lib/queries/territory";
import { isShieldActive, shieldUntilLabel } from "../../lib/shield";
import { terrainLabel } from "../../lib/terrain";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";
import StructureActions from "./StructureActions";

interface Props {
  playerId: string;
  cell: MapCellDetailsResponse;
  isHome: boolean;
  /** Територія клану гравця; null — гравець поза кланом. */
  territory: ClanTerritoryResponse | null | undefined;
  /** Ворожі марші, що йдуть саме на цю клітину. */
  threats: IncomingAttackResponse[];
  /** Табори на цій клітині — свої й чужі (§2.5). */
  camps: MapCampResponse[];
  onMarch: (target: { type: MarchTargetType; id: string; name: string; intent?: MarchIntent }) => void;
}

/**
 * Обрана клітина: місцевість і хто на ній стоїть. Кнопка нападу — лише на чуже;
 * своя споруда клану — підкріплення, вільна придатна клітина — закладання споруди.
 */
export default function CellDetails({ playerId, cell, isHome, territory, threats, camps, onMarch }: Props) {
  const catalog = useCatalog();
  const place = usePlaceStructure(playerId);
  const scout = useSendScout(playerId);
  const recall = useRecallCamp(playerId);
  const now = useNow();

  const occupant = cell.occupantType ?? null;
  const ownStructure =
    occupant === "ClanStructure" ? (territory?.structures.find((s) => s.id === cell.occupantId) ?? null) : null;
  const structureName = `Споруда клану${cell.occupantName == null ? "" : ` [${cell.occupantName}]`}`;
  const name =
    occupant === "ClanStructure"
      ? structureName
      : (cell.occupantName ?? (occupant === "Monster" ? "Монстр" : occupant === "Village" ? "Село" : ""));
  const targetType: MarchTargetType | null =
    occupant === "Monster"
      ? MARCH_TARGET.monster
      : occupant === "Village"
        ? MARCH_TARGET.village
        : occupant === "ClanStructure" && ownStructure === null
          ? MARCH_TARGET.clanStructure
          : null;

  // Слоти й очки перевіряє сервер; тут — лише чи варто показувати кнопку
  const canPlace =
    occupant === null && cell.habitable && territory != null && territory.enabled && territory.canBuild;
  const slotsLeft = territory == null ? 0 : territory.slotsOpen - territory.structures.length;

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-slate-800">
          {isHome ? "Ваше село" : occupant === null ? terrainLabel(cell.terrainType) : name}
        </h3>
        <span className="text-xs text-slate-500">
          ({cell.x}, {cell.y})
        </span>
      </div>

      <p className="text-sm text-slate-600">
        {terrainLabel(cell.terrainType)} · хід ×{cell.moveCost}
        {!cell.passable && " · непрохідна"}
      </p>

      {occupant === "Monster" && (
        <div className="space-y-1 text-sm">
          <p className="text-slate-700">Рівень {cell.monsterLevel ?? "?"}</p>
          {cell.monsterUnits !== null && cell.monsterUnits !== undefined && (
            <ul className="text-slate-600">
              {Object.entries(cell.monsterUnits).map(([unitType, count]) => (
                <li key={unitType}>
                  {catalog.unitName(unitType)} × {count}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {occupant === "Village" && isShieldActive(cell.shieldUntil) && (
        <p className="text-sm text-emerald-700">
          🛡 Нещодавно впало — під щитом до {shieldUntilLabel(cell.shieldUntil)}
        </p>
      )}

      {threats.length > 0 && (
        <ul className="space-y-1 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-900">
          {threats.map((threat) => (
            <li key={threat.marchId} className="flex justify-between gap-2">
              <span>
                {threat.intent === MARCH_INTENT.scout ? "👁" : "⚔"}{" "}
                {threat.attackerClanTag == null ? "" : `[${threat.attackerClanTag}] `}
                {threat.attackerName} {threat.intent === MARCH_INTENT.scout ? "розвідує" : "іде сюди"} з ({threat.fromX},{" "}
                {threat.fromY})
              </span>
              <span className="font-mono">{formatRemaining(threat.arrivesAt, now)}</span>
            </li>
          ))}
        </ul>
      )}

      {/* Одне місце для відмов розвідки й відкликання: кнопки розвідки є і в блоці табору, і в блоці села */}
      <ErrorBanner error={scout.error ?? recall.error} />

      {camps.length > 0 && (
        <ul className="space-y-2">
          {camps.map((camp) => {
            const own = camp.ownerPlayerId === playerId;
            const campName = `Табір ${camp.ownerClanTag == null ? "" : `[${camp.ownerClanTag}] `}${camp.ownerName}`;

            return (
              <li key={camp.marchId} className="space-y-2 rounded-lg bg-rose-50 p-3 text-sm">
                <p className="font-medium text-rose-900">⛺ {own ? "Ваш табір" : campName}</p>
                <p className="text-xs text-rose-800">
                  {own
                    ? "Армія стоїть у полі без стін — її можна атакувати. Відкличте, щоб повернути додому."
                    : "Чужа армія в полі: стін у неї немає, склад покаже розвідка."}
                </p>
                {own ? (
                  <button
                    type="button"
                    onClick={() => recall.mutate(camp.marchId)}
                    disabled={recall.isPending}
                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                  >
                    Відкликати
                  </button>
                ) : (
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => onMarch({ type: MARCH_TARGET.camp, id: camp.marchId, name: campName })}
                      className="flex-1 rounded-lg bg-rose-600 px-3 py-1.5 font-medium text-white hover:bg-rose-700"
                    >
                      Атакувати
                    </button>
                    <button
                      type="button"
                      onClick={() => scout.mutate({ targetType: MARCH_TARGET.camp, targetId: camp.marchId })}
                      disabled={scout.isPending}
                      className="flex-1 rounded-lg border border-amber-400 bg-white px-3 py-1.5 font-medium text-amber-800 hover:bg-amber-50 disabled:opacity-50"
                    >
                      Розвідати
                    </button>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}

      {ownStructure !== null && territory != null && (
        <StructureActions
          playerId={playerId}
          territory={territory}
          structure={ownStructure}
          onReinforce={() =>
            onMarch({ type: MARCH_TARGET.clanStructure, id: ownStructure.id, name, intent: MARCH_INTENT.reinforce })
          }
        />
      )}

      {canPlace && territory != null && (
        <div className="space-y-2 rounded-lg bg-slate-50 p-3 text-sm">
          <ErrorBanner error={place.error} />
          <p className="text-slate-600">
            Споруда клану: {territory.structureCost.toLocaleString("uk-UA")} очок вкладу (є{" "}
            {territory.points.toLocaleString("uk-UA")}) · вільних слотів {Math.max(slotsLeft, 0)}
          </p>
          <button
            type="button"
            onClick={() => place.mutate({ x: cell.x, y: cell.y })}
            disabled={place.isPending || slotsLeft <= 0 || territory.points < territory.structureCost}
            className="w-full rounded-lg bg-emerald-600 px-3 py-1.5 font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            {place.isPending ? "Закладаємо…" : "Закласти споруду"}
          </button>
        </div>
      )}

      {targetType !== null && !isHome && cell.occupantId !== null && cell.occupantId !== undefined && (
        <button
          type="button"
          data-tutorial="attack"
          onClick={() => onMarch({ type: targetType, id: cell.occupantId as string, name })}
          className="w-full rounded-lg bg-rose-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-rose-700"
        >
          Атакувати
        </button>
      )}

      {targetType !== null && targetType !== MARCH_TARGET.monster && !isHome && cell.occupantId != null && (
        <div className="space-y-1">
          <button
            type="button"
            onClick={() => scout.mutate({ targetType, targetId: cell.occupantId as string })}
            disabled={scout.isPending}
            className="w-full rounded-lg border border-amber-400 px-3 py-1.5 text-sm font-medium text-amber-800 hover:bg-amber-50 disabled:opacity-50"
          >
            {scout.isPending ? "Відправляємо…" : scout.isSuccess ? "Розвідники в дорозі" : "Розвідати"}
          </button>
        </div>
      )}
    </div>
  );
}
