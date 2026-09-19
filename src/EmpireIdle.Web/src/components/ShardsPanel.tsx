import { useState } from "react";
import { heroName } from "../lib/heroNames";
import type { HeroShardSummary } from "../lib/queries/heroes";

interface Props {
  shards: HeroShardSummary[];
  busy: boolean;
  onBuy: (heroKey: string, count: number) => void;
  onSummon: (heroKey: string) => void;
}

export default function ShardsPanel({ shards, busy, onBuy, onSummon }: Props) {
  const [counts, setCounts] = useState<Record<string, number>>({});

  if (shards.length === 0) {
    return <p className="text-sm text-slate-500">Уламків поки немає.</p>;
  }

  return (
    <div className="space-y-3">
      {shards.map((shard) => {
        const ready = shard.count >= shard.required;
        const count = counts[shard.heroKey] ?? 10;

        return (
          <div key={shard.heroKey} className="rounded-xl border border-slate-200 bg-white p-3">
            <div className="flex items-baseline justify-between">
              <span className="font-medium text-slate-800">{heroName(shard.heroKey)}</span>
              <span className="text-sm text-slate-600">
                {shard.count} / {shard.required}
              </span>
            </div>

            <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
              <div
                className={`h-full ${ready ? "bg-amber-500" : "bg-emerald-500"}`}
                style={{ width: `${Math.min(100, (shard.count / shard.required) * 100)}%` }}
              />
            </div>

            <div className="mt-3 flex items-center gap-2">
              <input
                type="number"
                min={1}
                max={100}
                value={count}
                onChange={(event) =>
                  setCounts((previous) => ({ ...previous, [shard.heroKey]: Number(event.target.value) }))
                }
                className="w-20 rounded-lg border border-slate-300 px-2 py-1 text-sm"
              />
              <button
                type="button"
                onClick={() => onBuy(shard.heroKey, count)}
                disabled={busy}
                className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                Купити за золото
              </button>
              {/* Призов ручний: гравець сам вирішує, коли витратити накопичене */}
              <button
                type="button"
                onClick={() => onSummon(shard.heroKey)}
                disabled={busy || !ready}
                className="ml-auto rounded-lg bg-amber-500 px-3 py-1 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
              >
                Призвати
              </button>
            </div>
          </div>
        );
      })}
    </div>
  );
}
