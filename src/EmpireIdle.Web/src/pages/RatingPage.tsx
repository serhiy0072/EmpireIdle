import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { useSession } from "../hooks/useSession";
import { compactPower, useLeaderboard, useMyRank, usePower } from "../lib/queries/rating";
import { formatNumber } from "../lib/format";
import { t } from "i18next";
import { Trans } from "react-i18next";

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
  return minutes < 1
    ? t("social:rating.justNow")
    : minutes < 60
      ? t("social:rating.minutesAgo", { count: minutes })
      : t("social:rating.hoursAgo", { count: Math.floor(minutes / 60) });
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
    return <p className="text-slate-500">{t("social:rating.loading")}</p>;
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
        <h1 className="text-xl font-medium text-slate-800">{t("social:rating.title")}</h1>
        <p className="text-xs text-slate-500">{t("social:rating.updated", { since: since(me.updatedAt) })}</p>
      </div>

      <section className="rounded-xl border border-amber-200 bg-white p-4">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="font-medium text-slate-800">
            <Trans ns="social" i18nKey="rating.place" values={{ rank: me.rank }} components={{ b: <span className="text-2xl text-amber-700" /> }} />
          </h2>
          <span className="text-sm text-slate-600">
            <Trans ns="social" i18nKey="rating.score" values={{ rating: formatNumber(me.rating) }} components={{ b: <span className="font-medium text-slate-800" /> }} />
          </span>
        </div>

        <div className="mt-3 grid gap-2 sm:grid-cols-3">
          <Stat
            label={t("social:rating.power")}
            value={power.data === undefined ? compactPower(me.powerScore) : compactPower(power.data.total)}
            hint={
              power.data === undefined
                ? undefined
                : t("village:power.breakdown", {
                    army: compactPower(power.data.army),
                    heroes: compactPower(power.data.hero),
                    gear: compactPower(power.data.equipment),
                  })
            }
          />
          <Stat label={t("social:rating.development")} value={formatNumber(Math.round(me.developmentScore))} hint={t("social:rating.developmentHint")} />
          <Stat label={t("social:rating.activity")} value={formatNumber(Math.round(me.activityScore))} hint={t("social:rating.activityHint")} />
        </div>

        <p className="mt-3 text-xs text-slate-500">
          {t("social:rating.stats", { monsters: me.monstersDefeated, battles: me.battlesWon, quests: me.questsCompleted })}
        </p>
      </section>

      <section className="overflow-hidden rounded-xl border border-slate-200 bg-white">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-3 py-2">#</th>
              <th className="px-3 py-2">{t("social:rating.player")}</th>
              <th className="px-3 py-2 text-right">{t("social:rating.rating")}</th>
              <th className="px-3 py-2 text-right">{t("social:rating.power")}</th>
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
                      <span className="ml-1 text-xs text-amber-700">{t("social:rating.you")}</span>
                    ) : (
                      <Link
                        to={`/chat?${new URLSearchParams({ tab: "Private", to: entry.playerId, name: entry.playerName }).toString()}`}
                        className="ml-2 text-xs font-normal text-emerald-700 hover:underline"
                      >
                        {t("social:rating.message")}
                      </Link>
                    )}
                  </td>
                  <td className="px-3 py-2 text-right text-slate-700">{formatNumber(entry.rating)}</td>
                  <td className="px-3 py-2 text-right text-slate-700">{compactPower(entry.power)}</td>
                </tr>
              );
            })}
            {top.data.length === 0 && (
              <tr>
                <td colSpan={4} className="px-3 py-4 text-center text-slate-500">
                  {t("social:rating.notYet")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>
    </div>
  );
}
