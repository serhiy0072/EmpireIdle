import { useVillage } from "../lib/queries/village";
import { formatNumber } from "../lib/format";

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
        Селище з'їдає <span className="font-semibold">{formatNumber(meal)} м'яса за годину</span> — що більше
        рівнів у будівель, то більше. Списується кожні 10 хвилин.
      </p>
      <p className="text-xs text-amber-800">
        Тримайте ферму в рості: забракне м'яса — село голодуватиме, і видобуток усього, крім м'яса, впаде на чверть.
      </p>
    </div>
  );
}
