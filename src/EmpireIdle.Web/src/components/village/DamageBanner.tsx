import type { VillageDamageResponse } from "../../lib/apiTypes";

interface Props {
  damage: VillageDamageResponse;
  damagedNames: string[];
  resourceName: (key: string) => string;
  busy: boolean;
  onRepair: () => void;
}

/**
 * Наслідки програних оборон (GDD §2.6): що пошкоджено, скільки поразок
 * лишилось до виселення і ремонт одразу за ресурси. Без ремонту будівлі
 * відновляться самі — банер зникне разом із пошкодженням.
 */
export default function DamageBanner({ damage, damagedNames, resourceName, busy, onRepair }: Props) {
  const left = Math.max(0, damage.defeatsToEvict - damage.defeatStreak);

  return (
    <div className="space-y-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-900">
      <p>
        Пошкоджено: {damagedNames.join(", ")}. Пошкоджені будівлі виробляють удвічі повільніше, а стіни
        захищають слабше.
      </p>
      <p>
        Поразок поспіль: {damage.defeatStreak}.{" "}
        {left === 1
          ? "Ще одна поразка до повного відновлення — і поселення виселять."
          : `До виселення — ще ${left} поразки без повного відновлення.`}
      </p>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-rose-800">
          Ремонт зараз: {damage.repairCost.map((c) => `${c.amount.toLocaleString("uk-UA")} ${resourceName(c.resource)}`).join(", ")}
        </span>
        <button
          type="button"
          onClick={onRepair}
          disabled={busy}
          className="rounded-lg bg-rose-600 px-3 py-1 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
        >
          Відремонтувати все
        </button>
      </div>
    </div>
  );
}
