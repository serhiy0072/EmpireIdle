import { useState } from "react";
import type { MarchTargetType, SendMarchRequest } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { useGarrison } from "../../lib/queries/garrison";
import { useHeroes } from "../../lib/queries/heroes";
import { BATTLE_ODDS, MARCH_INTENT, usePreviewMarch, useSendMarch } from "../../lib/queries/marches";
import { formatDuration, parseTimeSpan } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
  target: { type: MarchTargetType; id: string; name: string };
  onSent: () => void;
  onCancel: () => void;
}

/**
 * Відправка армії: склад із гарнізону, герой (обов'язковий — він і є слот
 * маршу), оцінка шансів до кліку. Прев'ю рахує сервер: клієнт не знає
 * ні пасивок, ні місцевості, ні щитів.
 */
export default function SendMarchForm({ playerId, target, onSent, onCancel }: Props) {
  const catalog = useCatalog();
  const garrison = useGarrison(playerId);
  const heroes = useHeroes(playerId);
  const preview = usePreviewMarch(playerId);
  const send = useSendMarch(playerId);

  const [counts, setCounts] = useState<Record<string, number>>({});
  const [heroId, setHeroId] = useState<string>("");

  const stacks = (garrison.data?.units ?? []).filter((stack) => stack.count > 0);
  const idleHeroes = (heroes.data?.heroes ?? []).filter((hero) => hero.state === "Idle");
  const chosenHero = heroId !== "" ? heroId : (idleHeroes[0]?.id ?? "");

  const units: Record<string, number> = {};

  for (const stack of stacks) {
    const key = `${stack.unitType}@${stack.level}`;
    const count = Math.min(Math.max(counts[key] ?? 0, 0), stack.count);

    if (count > 0) units[key] = count;
  }

  const ready = Object.keys(units).length > 0 && chosenHero !== "";

  const request = (): SendMarchRequest => ({
    targetType: target.type,
    targetId: target.id,
    units,
    heroId: chosenHero,
    intent: MARCH_INTENT.attack,
  });

  const odds = preview.data === undefined ? null : BATTLE_ODDS[preview.data.odds];

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-slate-800">Похід на {target.name}</h3>
        <button type="button" onClick={onCancel} className="text-sm text-slate-500 hover:underline">
          Скасувати
        </button>
      </div>

      <ErrorBanner error={preview.error ?? send.error} />

      {stacks.length === 0 ? (
        <p className="text-sm text-slate-500">У гарнізоні нікого: спершу навчіть юнітів у казармах.</p>
      ) : (
        <div className="space-y-2">
          <p className="text-xs font-medium text-slate-500">Хто йде</p>
          {stacks.map((stack) => {
            const key = `${stack.unitType}@${stack.level}`;
            const value = Math.min(Math.max(counts[key] ?? 0, 0), stack.count);

            return (
              <div key={key} className="flex items-center justify-between gap-2 text-sm">
                <span className="text-slate-700">
                  {catalog.unitName(stack.unitType)} (рів. {stack.level}) · є {stack.count}
                </span>
                <div className="flex items-center gap-1">
                  <input
                    type="number"
                    min={0}
                    max={stack.count}
                    value={value}
                    onChange={(event) => setCounts((prev) => ({ ...prev, [key]: Number(event.target.value) }))}
                    className="w-20 rounded-lg border border-slate-300 px-2 py-1 text-right"
                  />
                  <button
                    type="button"
                    onClick={() => setCounts((prev) => ({ ...prev, [key]: stack.count }))}
                    className="rounded-lg border border-slate-300 px-2 py-1 text-xs text-slate-600 hover:bg-slate-50"
                  >
                    усі
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <div className="space-y-1">
        <label htmlFor="march-hero" className="text-xs font-medium text-slate-500">
          Герой на чолі
        </label>
        {idleHeroes.length === 0 ? (
          <p className="text-sm text-slate-500">Немає вільного героя: кожен похід веде герой, що вдома.</p>
        ) : (
          <select
            id="march-hero"
            value={chosenHero}
            onChange={(event) => setHeroId(event.target.value)}
            className="w-full rounded-lg border border-slate-300 px-2 py-1 text-sm"
          >
            {idleHeroes.map((hero) => (
              <option key={hero.id} value={hero.id}>
                {catalog.heroName(hero.heroKey)} · рів. {hero.level}
                {hero.isLeader ? " · лідер" : ""}
              </option>
            ))}
          </select>
        )}
      </div>

      {preview.data !== undefined && odds !== null && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className={`rounded px-2 py-0.5 ${odds.className}`}>{odds.label}</span>
          <span className="text-slate-600">в дорозі {formatDuration(parseTimeSpan(preview.data.travelTime))}</span>
        </div>
      )}

      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => preview.mutate(request())}
          disabled={!ready || preview.isPending}
          className="flex-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          {preview.isPending ? "Оцінюємо…" : "Оцінити шанси"}
        </button>
        <button
          type="button"
          onClick={() => send.mutate(request(), { onSuccess: onSent })}
          disabled={!ready || send.isPending}
          className="flex-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          {send.isPending ? "Відправляємо…" : "Відправити"}
        </button>
      </div>
    </div>
  );
}
