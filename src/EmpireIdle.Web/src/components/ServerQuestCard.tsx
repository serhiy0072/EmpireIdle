import type { ServerQuestResponse } from "../lib/apiTypes";

interface Props {
  quest: ServerQuestResponse;
}

/** Стан серверного квесту приходить рядком — ім'ям QuestState на беку. */
const STATE_LABELS: Record<string, string> = {
  InProgress: "Триває",
  Completed: "Виконано",
  Claimed: "Нагороди роздано",
};

/**
 * Спільна ціль сервера: підсумок усіх гравців, власний внесок і місце.
 * Нагороду тут не забирають — її нараховує сервер за рангом (подія ServerQuestRewarded).
 */
export default function ServerQuestCard({ quest }: Props) {
  const ratio = quest.target > 0 ? Math.min(1, quest.total / quest.target) : 1;
  const finished = quest.state !== "InProgress";

  return (
    <div className={`space-y-3 rounded-xl border border-slate-200 p-4 ${finished ? "bg-slate-50" : "bg-white"}`}>
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-slate-800">{quest.displayName}</h3>
        <span className="text-xs text-slate-500">{STATE_LABELS[quest.state] ?? quest.state}</span>
      </div>

      <div>
        <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
          <div className={`h-full ${finished ? "bg-emerald-500" : "bg-sky-500"}`} style={{ width: `${ratio * 100}%` }} />
        </div>
        <p className="mt-1 text-xs text-slate-500">
          {quest.total.toLocaleString("uk-UA")} / {quest.target.toLocaleString("uk-UA")} — усім сервером
        </p>
      </div>

      <p className="text-sm text-slate-600">
        Ваш внесок: <span className="font-medium">{quest.myContribution.toLocaleString("uk-UA")}</span>
        {quest.myRank > 0 && (
          <>
            {" "}
            · місце <span className="font-medium">{quest.myRank}</span>
          </>
        )}
      </p>
    </div>
  );
}
