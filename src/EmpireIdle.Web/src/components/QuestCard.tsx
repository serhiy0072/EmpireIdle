import type { QuestView } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { QUEST_STATE } from "../lib/queries/quests";
import { objectiveIsThreshold, objectiveLabel, rewardLabel } from "../lib/questLabels";

interface Props {
  quest: QuestView;
  busy: boolean;
  onClaim: () => void;
}

/** Квест: цілі з прогресом, нагороди й кнопка, коли є що забрати. */
export default function QuestCard({ quest, busy, onClaim }: Props) {
  const catalog = useCatalog();

  const completed = quest.state === QUEST_STATE.completed;
  const claimed = quest.state === QUEST_STATE.claimed;

  return (
    <div
      className={`space-y-3 rounded-xl border p-4 ${
        completed ? "border-amber-300 bg-amber-50" : claimed ? "border-slate-200 bg-slate-50 opacity-60" : "border-slate-200 bg-white"
      }`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-slate-800">{quest.displayName}</h3>
        {claimed && <span className="text-xs text-slate-500">Отримано</span>}
      </div>

      <ul className="space-y-2">
        {quest.objectives.map((objective, index) => {
          const ratio = objective.required > 0 ? Math.min(1, objective.amount / objective.required) : 1;
          const done = objective.amount >= objective.required;

          return (
            <li key={index} className="text-sm">
              <div className="flex items-baseline justify-between gap-2">
                <span className={done ? "text-emerald-700" : "text-slate-700"}>
                  {done && "✓ "}
                  {objectiveLabel(objective, catalog)}
                </span>
                {!objectiveIsThreshold(objective) && (
                  <span className="text-xs text-slate-500">
                    {Math.min(objective.amount, objective.required).toLocaleString("uk-UA")} /{" "}
                    {objective.required.toLocaleString("uk-UA")}
                  </span>
                )}
              </div>
              {!objectiveIsThreshold(objective) && (
                <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-slate-100">
                  <div className={`h-full ${done ? "bg-emerald-500" : "bg-sky-500"}`} style={{ width: `${ratio * 100}%` }} />
                </div>
              )}
            </li>
          );
        })}
      </ul>

      <div className="flex flex-wrap items-center gap-1">
        {quest.rewards.map((reward, index) => (
          <span key={index} className="rounded bg-violet-100 px-2 py-0.5 text-xs text-violet-800">
            {rewardLabel(reward, catalog)}
          </span>
        ))}

        {completed && (
          <button
            type="button"
            onClick={onClaim}
            disabled={busy}
            className="ml-auto rounded-lg bg-amber-500 px-3 py-1 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
          >
            Забрати
          </button>
        )}
      </div>
    </div>
  );
}
