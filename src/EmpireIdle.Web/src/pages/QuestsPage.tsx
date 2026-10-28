import ErrorBanner from "../components/ErrorBanner";
import QuestCard from "../components/QuestCard";
import ServerQuestCard from "../components/ServerQuestCard";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { QuestView, QuestWindow } from "../lib/apiTypes";
import { nextDailyResetAt, QUEST_STATE, QUEST_WINDOW, useClaimAllQuests, useClaimQuest, useQuests, useServerQuests } from "../lib/queries/quests";
import { formatRemaining } from "../lib/time";
import { t } from "i18next";

/** Порядок секцій: спершу те, що обнуляється, — щоденні горять. */
const SECTIONS: { window: QuestWindow; title: "daily" | "events" | "story" }[] = [
  { window: QUEST_WINDOW.daily, title: "daily" },
  { window: QUEST_WINDOW.event, title: "events" },
  { window: QUEST_WINDOW.chain, title: "story" },
];

/** Готові до отримання — нагорі: за ними гравець і прийшов. Далі активні, отримані — в кінці. */
function byUrgency(a: QuestView, b: QuestView): number {
  const weight = (quest: QuestView) =>
    quest.state === QUEST_STATE.completed ? 0 : quest.state === QUEST_STATE.inProgress ? 1 : 2;

  return weight(a) - weight(b);
}

export default function QuestsPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();

  const quests = useQuests(playerId);
  const serverQuests = useServerQuests(playerId);
  const claim = useClaimQuest(playerId);
  const claimAll = useClaimAllQuests(playerId);

  if (quests.isPending) {
    return <p className="text-slate-500">{t("social:quests.loading")}</p>;
  }

  if (quests.isError) {
    return <ErrorBanner error={quests.error} />;
  }

  const claimable = quests.data.filter((quest) => quest.state === QUEST_STATE.completed).length;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">{t("social:quests.title")}</h1>
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-sm text-slate-500">
            {claimable > 0 ? `${t("social:quests.claimable", { count: claimable })} · ` : ""}
            {t("social:quests.dailyReset", { remaining: formatRemaining(nextDailyResetAt(now), now) })}
          </p>
          {claimable > 1 && (
            <button
              type="button"
              onClick={() => claimAll.mutate()}
              disabled={claim.isPending || claimAll.isPending}
              className="rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              {t("social:quests.claimAll")}
            </button>
          )}
        </div>
      </div>

      <ErrorBanner error={claim.error ?? claimAll.error} />

      {SECTIONS.map((section) => {
        const items = quests.data.filter((quest) => quest.window === section.window).sort(byUrgency);

        if (items.length === 0) return null;

        return (
          <section key={section.window} className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t(`social:quests.section.${section.title}`)}</h2>
            <div className="grid gap-3 sm:grid-cols-2">
              {items.map((quest) => (
                <QuestCard
                  key={quest.key}
                  quest={quest}
                  busy={claim.isPending || claimAll.isPending}
                  onClaim={() => claim.mutate(quest.key)}
                />
              ))}
            </div>
          </section>
        );
      })}

      {quests.data.length === 0 && <p className="text-sm text-slate-500">{t("social:quests.none")}</p>}

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{t("social:quests.server")}</h2>

        {serverQuests.isPending ? (
          <p className="text-sm text-slate-500">{t("social:quests.serverLoading")}</p>
        ) : serverQuests.isError ? (
          <ErrorBanner error={serverQuests.error} />
        ) : serverQuests.data.length === 0 ? (
          <p className="text-sm text-slate-500">{t("social:quests.serverNone")}</p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {serverQuests.data.map((quest) => (
              <ServerQuestCard key={quest.key} quest={quest} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
