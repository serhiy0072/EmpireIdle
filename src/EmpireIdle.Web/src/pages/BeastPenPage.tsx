import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import BeastEnclosure from "../components/beasts/BeastEnclosure";
import BeastListPopup from "../components/beasts/BeastListPopup";
import { percent } from "../components/beasts/beastText";
import ErrorBanner from "../components/ErrorBanner";
import HungerBanner from "../components/HungerBanner";
import { useSession } from "../hooks/useSession";
import { FEED_ITEM, useBeastPen, useFindTameTarget } from "../lib/queries/beasts";
import { useCatalog } from "../lib/queries/catalog";
import { useInventory } from "../lib/queries/inventory";

/**
 * Звіринець (GDD §5.10): приручені звірі й кого ще можна приручити. Годування й пасивка — на екрані
 * звіра (/beasts/:key). Приручають на мапі — маршем із наміром «Приручити» на монстра.
 */
export default function BeastPenPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const pen = useBeastPen(playerId);
  const inventory = useInventory(playerId);
  const find = useFindTameTarget(playerId);
  const navigate = useNavigate();
  // Звір, якого шукали, а монстра його типу у світі зараз немає
  const [missing, setMissing] = useState<string | null>(null);
  const [list, setList] = useState(false);

  if (pen.isPending) return <p className="text-sm text-slate-500">Завантаження…</p>;
  if (pen.isError) return <ErrorBanner error={pen.error} />;

  const feedLeft = inventory.data?.items.find((item) => item.itemKey === FEED_ITEM)?.count ?? 0;
  const tamedKeys = new Set(pen.data.beasts.map((beast) => beast.beastKey));

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Звіринець</h1>
        <p className="text-sm text-slate-600">
          Видів {pen.data.beasts.length} з {pen.data.capacity} · ласощів {feedLeft.toLocaleString("uk-UA")}
          {pen.data.fodderUpkeepPerHour > 0 && (
            <> · їсть {pen.data.fodderUpkeepPerHour.toLocaleString("uk-UA")} корму/год</>
          )}
        </p>
      </div>

      {/* Звіринець їсть зі складу й незібраного виробітку ферм (рішення 13.10.2026) */}
      {pen.data.isHungry && <HungerBanner kind="beasts" />}

      <ErrorBanner error={find.error} />

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
        // Загін за референсом 21.10.2026: звірі бродять, натиск — екран звіра; список — для тих, хто не влучив
        <div className="relative">
          <BeastEnclosure beasts={pen.data.beasts} onSelect={(key) => void navigate(`/beasts/${key}`)} />
          <button
            type="button"
            onClick={() => setList(true)}
            className="absolute bottom-3 left-3 rounded-xl bg-gradient-to-b from-sky-400 to-blue-600 px-3 py-1.5 text-sm font-semibold text-white shadow ring-1 ring-sky-200 hover:brightness-110"
          >
            📋 Список звірів
          </button>
        </div>
      )}

      {list && (
        <BeastListPopup
          beasts={pen.data.beasts}
          name={catalog.beastName}
          onPick={(key) => void navigate(`/beasts/${key}`)}
          onClose={() => setList(false)}
        />
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
              <span className="flex items-center gap-3">
                <span className="text-xs text-slate-500">
                  {missing === taming.beastKey
                    ? "у світі зараз жодного — спробуйте пізніше"
                    : `шанс ${percent(taming.chance)} · гарантія через ${Math.max(taming.pityWins - taming.misses, 0) + 1} перемог`}
                </span>
                {/* Найближчий монстр цього звіра — і одразу форма приручення на мапі (рішення 21.10.2026) */}
                {pen.data.capacity > 0 && (
                  <button
                    type="button"
                    onClick={() =>
                      find.mutate(taming.beastKey, {
                        onSuccess: (target) => {
                          setMissing(target === null ? taming.beastKey : null);
                          if (target !== null)
                            void navigate(
                              `/map?focus=${target.x},${target.y},${target.monsterId}&tame=${target.monsterId}&beast=${taming.beastKey}`,
                            );
                        },
                      })
                    }
                    disabled={find.isPending}
                    className="rounded-lg border border-amber-400 px-2 py-0.5 text-xs font-medium text-amber-800 hover:bg-amber-50 disabled:opacity-50"
                  >
                    🎯 Знайти
                  </button>
                )}
              </span>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
