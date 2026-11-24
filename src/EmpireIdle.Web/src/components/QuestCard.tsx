import type { QuestView } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { QUEST_STATE } from "../lib/queries/quests";
import { objectiveIsThreshold, objectiveLabel, rewardLabel } from "../lib/questLabels";
import { formatNumber } from "../lib/format";
import { t } from "i18next";

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
        completed ? "border-amber-400/50 bg-amber-500/15" : claimed ? "border-[#335c9a] bg-white/5 opacity-60" : "border-[#335c9a] bg-[#1d3b6f]"
      }`}
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-white">{quest.displayName}</h3>
        {claimed && <span className="text-xs text-sky-100/70">{t("social:quests.claimed")}</span>}
      </div>

      <ul className="space-y-2">
        {quest.objectives.map((objective, index) => {
          const ratio = objective.required > 0 ? Math.min(1, objective.amount / objective.required) : 1;
          const done = objective.amount >= objective.required;

          return (
            <li key={index} className="text-sm">
              <div className="flex items-baseline justify-between gap-2">
                <span className={done ? "text-emerald-200" : "text-slate-100"}>
                  {done && "✓ "}
                  {objectiveLabel(objective, catalog)}
                </span>
                {!objectiveIsThreshold(objective) && (
                  <span className="text-xs text-sky-100/70">
                    {formatNumber(Math.min(objective.amount, objective.required))} /{" "}
                    {formatNumber(objective.required)}
                  </span>
                )}
              </div>
              {!objectiveIsThreshold(objective) && (
                <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-white/10">
                  <div className={`h-full ${done ? "bg-emerald-500" : "bg-sky-500"}`} style={{ width: `${ratio * 100}%` }} />
                </div>
              )}
            </li>
          );
        })}
      </ul>

      <div className="flex flex-wrap items-center gap-1">
        {quest.rewards.map((reward, index) => (
          <span key={index} className="rounded bg-violet-500/25 px-2 py-0.5 text-xs text-violet-200">
            {rewardLabel(reward, catalog)}
          </span>
        ))}

        {completed && (
          <button
            type="button"
            data-tutorial={`claim:${quest.key}`}
            onClick={onClaim}
            disabled={busy}
            className="ml-auto rounded-lg bg-amber-500 px-3 py-1 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
          >
            {t("social:quests.claim")}
          </button>
        )}
      </div>
    </div>
  );
}
