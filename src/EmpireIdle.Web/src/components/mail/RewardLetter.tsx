import type { MailLetterView, MailRewardView } from "../../lib/queries/mail";
import type { Catalog } from "../../lib/queries/catalog";

interface Props {
  letter: MailLetterView;
  catalog: Catalog;
  busy: boolean;
  onClaim: () => void;
  onOpen: () => void;
}

const TITLES: Record<string, (letter: MailLetterView) => string> = {
  DailyReward: (letter) => `Щоденна нагорода — день ${letter.sequence ?? 1}`,
  WeeklyReward: () => "Нагорода тижня",
  MonthlyReward: () => "Нагорода місяця",
};

const date = (iso: string) =>
  new Date(iso).toLocaleString("uk-UA", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

/** Рядок нагороди мовою гравця: gems, ресурс чи предмет. */
function rewardLabel(reward: MailRewardView, catalog: Catalog): string {
  const type = reward.type.toLowerCase();
  const amount = reward.amount.toLocaleString("uk-UA");

  if (type === "gems") return `💎 ${amount}`;
  if (type === "resource" && reward.key != null) return `${catalog.resourceName(reward.key)} × ${amount}`;
  if (reward.key != null) return `${catalog.itemName(reward.key)} × ${amount}`;

  return `${reward.type} × ${amount}`;
}

/**
 * Лист із нагородою (GDD §7.4): вкладення, строк і кнопка «Забрати».
 * Незабране згорає разом із листом — строк видно заздалегідь.
 */
export default function RewardLetter({ letter, catalog, busy, onClaim, onOpen }: Props) {
  const title = TITLES[letter.kind]?.(letter) ?? "Нагорода";

  return (
    <li
      className="space-y-2 rounded-xl border border-amber-200 bg-white p-3"
      onMouseEnter={() => !letter.isRead && onOpen()}
      onClick={() => !letter.isRead && onOpen()}
    >
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="font-medium text-slate-800">
          {!letter.isRead && (
            <span className="mr-1 inline-block h-2 w-2 rounded-full bg-emerald-500" aria-label="непрочитане" />
          )}
          🎁 {title}
        </span>
        <span className="text-xs text-slate-400">{date(letter.createdAt)}</span>
      </div>

      <ul className="flex flex-wrap gap-2 text-sm">
        {letter.rewards.map((reward) => (
          <li key={`${reward.type}:${reward.key ?? ""}`} className="rounded-lg bg-amber-50 px-2 py-0.5 text-amber-900">
            {rewardLabel(reward, catalog)}
          </li>
        ))}
      </ul>

      <div className="flex items-center justify-between gap-2">
        {letter.claimedAt != null ? (
          <span className="text-xs text-slate-500">Отримано {date(letter.claimedAt)}</span>
        ) : (
          <span className="text-xs text-slate-500">Згорить {date(letter.expiresAt)}</span>
        )}

        {letter.canClaim && (
          <button
            type="button"
            disabled={busy}
            onClick={(event) => {
              event.stopPropagation();
              onClaim();
            }}
            className="rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Забрати
          </button>
        )}
      </div>
    </li>
  );
}
