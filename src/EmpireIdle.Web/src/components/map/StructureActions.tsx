import { useState } from "react";
import { useNow } from "../../hooks/useNow";
import type { ClanStructureResponse, ClanTerritoryResponse } from "../../lib/apiTypes";
import { useDemolishStructure, useRecallFromStructure } from "../../lib/queries/territory";
import { formatRemaining } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
  territory: ClanTerritoryResponse;
  structure: ClanStructureResponse;
  onReinforce: () => void;
}

/**
 * Своя споруда клану на мапі: стан будівництва, гарнізон, і що з нею можна зробити.
 * Знесення незворотне й очок не повертає — тож двоклікове, без браузерного confirm.
 */
export default function StructureActions({ playerId, territory, structure, onReinforce }: Props) {
  const now = useNow();
  const recall = useRecallFromStructure(playerId);
  const demolish = useDemolishStructure(playerId);
  const [confirming, setConfirming] = useState(false);

  const building = Date.parse(structure.readyAt) > now;

  return (
    <div className="space-y-2 text-sm">
      <ErrorBanner error={recall.error ?? demolish.error} />

      {building ? (
        <p className="text-amber-800">
          Будується: ще {formatRemaining(structure.readyAt, now)}
          {structure.acceleratedShare > 0 && ` · марші зрізали ${Math.round(structure.acceleratedShare * 100)}%`}
        </p>
      ) : (
        <p className="text-emerald-700">Діє: бонус клану в радіусі {territory.radius} клітин.</p>
      )}

      <p className="text-slate-600">
        Гарнізон {structure.garrisonUnits} / {territory.garrisonCapacity}
        {structure.myUnits > 0 && ` · ваших ${structure.myUnits}`}
      </p>

      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={onReinforce}
          className="flex-1 rounded-lg bg-emerald-600 px-3 py-1.5 font-medium text-white hover:bg-emerald-700"
        >
          {building ? "Допомогти будувати" : "Відправити гарнізон"}
        </button>
        {structure.myUnits > 0 && (
          <button
            type="button"
            onClick={() => recall.mutate(structure.id)}
            disabled={recall.isPending}
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            {recall.isPending ? "Кличемо…" : "Забрати своїх"}
          </button>
        )}
      </div>

      {territory.canBuild &&
        (confirming ? (
          <div className="flex items-center gap-2 rounded-lg bg-rose-50 px-3 py-2">
            <span className="flex-1 text-rose-900">Знести? Гарнізон піде додому, очки не повернуться.</span>
            <button
              type="button"
              onClick={() => demolish.mutate(structure.id, { onSettled: () => setConfirming(false) })}
              disabled={demolish.isPending}
              className="rounded-lg bg-rose-600 px-3 py-1 font-medium text-white hover:bg-rose-700 disabled:opacity-50"
            >
              Знести
            </button>
            <button type="button" onClick={() => setConfirming(false)} className="px-1 text-rose-900 hover:underline">
              Ні
            </button>
          </div>
        ) : (
          <button type="button" onClick={() => setConfirming(true)} className="text-xs text-rose-700 hover:underline">
            Знести споруду
          </button>
        ))}
    </div>
  );
}
