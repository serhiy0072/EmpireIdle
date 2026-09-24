import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { useSession } from "../hooks/useSession";
import { compactPower, useLeaderboard, useMyRank, usePower } from "../lib/queries/rating";

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="rounded-lg bg-slate-50 px-3 py-2" title={hint}>
      <div className="text-xs text-slate-500">{label}</div>
      <div className="font-medium text-slate-800">{value}</div>
    </div>
  );
}

function since(iso: string): string {
  const minutes = Math.max(0, Math.round((Date.now() - Date.parse(iso)) / 60_000));
  return minutes < 1 ? "щойно" : minutes < 60 ? `${minutes} хв тому` : `${Math.floor(minutes / 60)} год тому`;
}

/**
 * Рейтинг світу: моє місце з розкладкою й топ гравців. Рейтинг рахується
 * щогодини з сили, розвитку й активності, тож може відставати від сили.
 */
export default function RatingPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const rank = useMyRank(playerId);
  const power = usePower(playerId);
  const top = useLeaderboard();

  if (rank.isPending || top.isPending) {
    return <p className="text-slate-500">Рахуємо рейтинг…</p>;
  }

  if (rank.isError) {
    return <ErrorBanner error={rank.error} />;
  }

  if (top.isError) {
    return <ErrorBanner error={top.error} />;
  }

  const me = rank.data;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Рейтинг світу</h1>
        <p className="text-xs text-slate-500">Перераховується щогодини · оновлено {since(me.updatedAt)}</p>
      </div>

      <section className="rounded-xl border border-amber-200 bg-white p-4">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="font-medium text-slate-800">
            Ваше місце: <span className="text-2xl text-amber-700">#{me.rank}</span>
          </h2>
          <span className="text-sm text-slate-600">
            рейтинг <span className="font-medium text-slate-800">{me.rating.toLocaleString("uk-UA")}</span>
          </span>
        </div>

        <div className="mt-3 grid gap-2 sm:grid-cols-3">
          <Stat
            label="⚔ Сила"
            value={power.data === undefined ? compactPower(me.powerScore) : compactPower(power.data.total)}
            hint={
              power.data === undefined
                ? undefined
                : `Армія ${compactPower(power.data.army)} · герої ${compactPower(power.data.hero)} · спорядження ${compactPower(power.data.equipment)}`
            }
          />
          <Stat label="🏗 Розвиток" value={Math.round(me.developmentScore).toLocaleString("uk-UA")} hint="Рівні будівель села" />
          <Stat label="🔥 Активність" value={Math.round(me.activityScore).toLocaleString("uk-UA")} hint="Бої, монстри, квести, внески" />
        </div>

        <p className="mt-3 text-xs text-slate-500">
          Монстрів переможено {me.monstersDefeated} · боїв виграно {me.battlesWon} · квестів виконано {me.questsCompleted}
        </p>
      </section>

      <section className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-3 py-2">#</th>
              <th className="px-3 py-2">Гравець</th>
              <th className="px-3 py-2 text-right">Рейтинг</th>
              <th className="px-3 py-2 text-right">⚔ Сила</th>
            </tr>
          </thead>
          <tbody>
            {top.data.map((entry) => {
              const mine = entry.playerId === playerId;
              return (
                <tr key={entry.playerId} className={`border-t border-slate-100 ${mine ? "bg-amber-50" : ""}`}>
                  <td className="px-3 py-2 text-slate-500">{entry.rank}</td>
                  <td className="px-3 py-2 font-medium text-slate-800">
                    {entry.playerName}
                    {mine ? (
                      <span className="ml-1 text-xs text-amber-700">(ви)</span>
                    ) : (
                      <Link
                        to={`/chat?${new URLSearchParams({ tab: "Private", to: entry.playerId, name: entry.playerName }).toString()}`}
                        className="ml-2 text-xs font-normal text-emerald-700 hover:underline"
                      >
                        написати
                      </Link>
                    )}
                  </td>
                  <td className="px-3 py-2 text-right text-slate-700">{entry.rating.toLocaleString("uk-UA")}</td>
                  <td className="px-3 py-2 text-right text-slate-700">{compactPower(entry.power)}</td>
                </tr>
              );
            })}
            {top.data.length === 0 && (
              <tr>
                <td colSpan={4} className="px-3 py-4 text-center text-slate-500">
                  Топ ще не сформовано — перший перерахунок за годину.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>
    </div>
  );
}
