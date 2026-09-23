import ErrorBanner from "../components/ErrorBanner";
import QuestCard from "../components/QuestCard";
import ServerQuestCard from "../components/ServerQuestCard";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { QuestView, QuestWindow } from "../lib/apiTypes";
import { nextDailyResetAt, QUEST_STATE, QUEST_WINDOW, useClaimQuest, useQuests, useServerQuests } from "../lib/queries/quests";
import { formatRemaining } from "../lib/time";

/** Порядок секцій: спершу те, що обнуляється, — щоденні горять. */
const SECTIONS: { window: QuestWindow; title: string }[] = [
  { window: QUEST_WINDOW.daily, title: "Щоденні" },
  { window: QUEST_WINDOW.event, title: "Події" },
  { window: QUEST_WINDOW.chain, title: "Історія" },
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

  if (quests.isPending) {
    return <p className="text-slate-500">Завантаження квестів…</p>;
  }

  if (quests.isError) {
    return <ErrorBanner error={quests.error} />;
  }

  const claimable = quests.data.filter((quest) => quest.state === QUEST_STATE.completed).length;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Квести</h1>
        <p className="text-sm text-slate-500">
          {claimable > 0 ? `Готово до отримання: ${claimable} · ` : ""}
          щоденні оновляться за {formatRemaining(nextDailyResetAt(now), now)}
        </p>
      </div>

      <ErrorBanner error={claim.error} />

      {SECTIONS.map((section) => {
        const items = quests.data.filter((quest) => quest.window === section.window).sort(byUrgency);

        if (items.length === 0) return null;

        return (
          <section key={section.window} className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">{section.title}</h2>
            <div className="grid gap-3 sm:grid-cols-2">
              {items.map((quest) => (
                <QuestCard
                  key={quest.key}
                  quest={quest}
                  busy={claim.isPending}
                  onClaim={() => claim.mutate(quest.key)}
                />
              ))}
            </div>
          </section>
        );
      })}

      {quests.data.length === 0 && <p className="text-sm text-slate-500">Наразі завдань немає.</p>}

      <section className="space-y-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Серверні</h2>

        {serverQuests.isPending ? (
          <p className="text-sm text-slate-500">Завантаження…</p>
        ) : serverQuests.isError ? (
          <ErrorBanner error={serverQuests.error} />
        ) : serverQuests.data.length === 0 ? (
          <p className="text-sm text-slate-500">Спільних цілей сервера зараз немає.</p>
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
