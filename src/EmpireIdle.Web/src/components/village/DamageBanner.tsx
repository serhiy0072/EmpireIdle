import type { VillageDamageResponse } from "../../lib/apiTypes";
import { formatNumber } from "../../lib/format";
import { t } from "i18next";

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
        {t("village:damage.list", { buildings: damagedNames.join(", ") })}
      </p>
      <p>
        {t("village:damage.streak", { count: damage.defeatStreak })}{" "}
        {left === 1 ? t("village:damage.lastChance") : t("village:damage.left", { count: left })}
      </p>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-rose-800">
          {t("village:damage.repairCost", {
            cost: damage.repairCost.map((c) => `${formatNumber(c.amount)} ${resourceName(c.resource)}`).join(", "),
          })}
        </span>
        <button
          type="button"
          onClick={onRepair}
          disabled={busy}
          className="rounded-lg bg-rose-600 px-3 py-1 text-sm font-medium text-white hover:bg-rose-700 disabled:opacity-50"
        >
          {t("village:damage.repairAll")}
        </button>
      </div>
    </div>
  );
}
