import { useState } from "react";
import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { useActivateBeast, useBeastPen, useFeedBeast, type BeastResponse } from "../lib/queries/beasts";
import { useCatalog } from "../lib/queries/catalog";
import { useInventory } from "../lib/queries/inventory";
import { formatRemaining } from "../lib/time";

/** Ключ корму з конфіга — той самий, що в beasts.json. */
const FEED_ITEM = "beast_feed";

const EFFECT_LABELS: Record<string, string> = {
  Production: "виробіток",
  Attack: "атака",
  Defense: "захист",
  MarchSpeed: "швидкість маршів",
  Carry: "вантажопідйомність",
};

const percent = (value: number) => `${Math.round(value * 1000) / 10}%`;

/**
 * Звіринець (GDD §5.10): приручені звірі, годування кормом і пасивки за їжу.
 * Приручають на мапі — маршем із наміром «Приручити» на монстра.
 */
export default function BeastPenPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const now = useNow();

  const pen = useBeastPen(playerId);
  const inventory = useInventory(playerId);
  const feed = useFeedBeast(playerId);
  const activate = useActivateBeast(playerId);

  if (pen.isPending) return <p className="text-sm text-slate-500">Завантаження…</p>;
  if (pen.isError) return <ErrorBanner error={pen.error} />;

  const feedLeft = inventory.data?.items.find((item) => item.itemKey === FEED_ITEM)?.count ?? 0;
  const tamedKeys = new Set(pen.data.beasts.map((beast) => beast.beastKey));
  const busy = feed.isPending || activate.isPending;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Звіринець</h1>
        <p className="text-sm text-slate-600">
          Видів {pen.data.beasts.length} з {pen.data.capacity} · корму {feedLeft.toLocaleString("uk-UA")}
        </p>
      </div>

      <ErrorBanner error={feed.error ?? activate.error} />

      {pen.data.capacity === 0 ? (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Звіринець ще не відкритий — підніміть ратушу, щоб приручати звірів.
        </p>
      ) : pen.data.beasts.length === 0 ? (
        <p className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600">
          Тут поки порожньо. Знайдіть монстра на <Link to="/map" className="text-emerald-700 hover:underline">мапі</Link> й
          відправте похід із наміром «Приручити».
        </p>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {pen.data.beasts.map((beast) => (
            <BeastCard
              key={beast.beastKey}
              beast={beast}
              name={catalog.beastName(beast.beastKey)}
              now={now}
              feedLeft={feedLeft}
              busy={busy}
              onFeed={(count) => feed.mutate({ beastKey: beast.beastKey, count })}
              onActivate={() => activate.mutate(beast.beastKey)}
            />
          ))}
        </div>
      )}

      <section className="space-y-2">
        <h2 className="text-sm font-medium text-slate-700">Кого можна приручити</h2>
        <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white text-sm">
          {pen.data.taming.map((taming) => (
            <li key={taming.beastKey} className="flex flex-wrap items-baseline justify-between gap-2 px-3 py-2">
              <span className="text-slate-800">
                {catalog.beastName(taming.beastKey)}
                {tamedKeys.has(taming.beastKey) && <span className="ml-1 text-xs text-emerald-700">· уже є</span>}
              </span>
              <span className="text-xs text-slate-500">
                шанс {percent(taming.chance)} · гарантія через {Math.max(taming.pityWins - taming.misses, 0) + 1} перемог
              </span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

interface CardProps {
  beast: BeastResponse;
  name: string;
  now: number;
  feedLeft: number;
  busy: boolean;
  onFeed: (count: number) => void;
  onActivate: () => void;
}

function BeastCard({ beast, name, now, feedLeft, busy, onFeed, onActivate }: CardProps) {
  const [count, setCount] = useState(1);

  const { passive } = beast;
  const active = passive.activeUntil != null && Date.parse(passive.activeUntil) > now;
  const cooling = passive.cooldownUntil != null && Date.parse(passive.cooldownUntil) > now;
  const capped = beast.level >= beast.maxLevel;
  const toNext = beast.experienceToNext - beast.experience;

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="font-medium text-slate-800">{name}</h2>
        <span className="text-xs text-slate-500">
          ранг {beast.rank} · рівень {beast.level} з {beast.maxLevel}
        </span>
      </div>

      <div className="space-y-1">
        <div className="h-2 overflow-hidden rounded bg-slate-100">
          <div
            className="h-full bg-amber-400"
            style={{ width: capped ? "100%" : `${Math.min(100, (beast.experience / beast.experienceToNext) * 100)}%` }}
          />
        </div>
        <p className="text-xs text-slate-500">
          {capped
            ? "Стеля рангу: далі веде лише ще один такий самий звір"
            : `До наступного рівня — ${toNext.toLocaleString("uk-UA")} корму`}
        </p>
      </div>

      {!capped && (
        <div className="flex items-center gap-2">
          <input
            type="number"
            min={1}
            max={Math.max(feedLeft, 1)}
            value={count}
            onChange={(event) => setCount(Math.max(1, Number(event.target.value)))}
            className="w-24 rounded-lg border border-slate-300 px-2 py-1 text-right text-sm"
          />
          <button
            type="button"
            onClick={() => setCount(Math.max(1, Math.min(feedLeft, toNext)))}
            className="rounded-lg border border-slate-300 px-2 py-1 text-xs text-slate-600 hover:bg-slate-50"
          >
            до рівня
          </button>
          <button
            type="button"
            onClick={() => onFeed(count)}
            disabled={busy || feedLeft < count}
            className="flex-1 rounded-lg border border-amber-300 px-3 py-1 text-sm text-amber-800 hover:bg-amber-50 disabled:opacity-50"
          >
            Нагодувати
          </button>
        </div>
      )}

      <div className="space-y-2 rounded-lg bg-slate-50 p-3 text-sm">
        <p className="text-slate-700">
          +{percent(passive.bonus)} {EFFECT_LABELS[passive.effect] ?? passive.effect} на{" "}
          {Math.round(passive.durationMinutes / 60)} год
        </p>
        {active ? (
          <p className="text-emerald-700">Діє ще {formatRemaining(passive.activeUntil!, now)}</p>
        ) : cooling ? (
          <p className="text-slate-500">Відпочиває ще {formatRemaining(passive.cooldownUntil!, now)}</p>
        ) : (
          <button
            type="button"
            onClick={onActivate}
            disabled={busy}
            className="w-full rounded-lg bg-emerald-600 px-3 py-1.5 font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Активувати за {passive.activationFood.toLocaleString("uk-UA")} їжі
          </button>
        )}
      </div>
    </div>
  );
}
