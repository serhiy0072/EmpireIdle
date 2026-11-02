import { useVillage } from "../lib/queries/village";
import { formatNumber } from "../lib/format";
import { Trans } from "react-i18next";
import { t } from "i18next";

/**
 * Їдальня (рішення 20.10.2026): селище щогодини їсть м'ясо — 15 за кожен рівень будівель із рівнями.
 * Тут видно, скільки саме, і чому ферма мусить рости разом із селом.
 */
export default function MessHallPanel({ playerId }: { playerId: string }) {
  const village = useVillage(playerId);
  const meal = village.data?.mealPerHour ?? 0;

  return (
    <div className="space-y-1 rounded-lg bg-amber-50 p-2 text-sm text-amber-900">
      <p>
        <Trans
          ns="village"
          i18nKey="messHall.meal"
          values={{ amount: formatNumber(meal) }}
          components={{ b: <span className="font-semibold" /> }}
        />
      </p>
      <p className="text-xs text-amber-800">
        {t("village:messHall.hint")}
      </p>
    </div>
  );
}
