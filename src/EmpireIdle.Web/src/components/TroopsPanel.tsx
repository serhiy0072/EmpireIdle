import { useCatalog } from "../lib/queries/catalog";
import { useGarrison } from "../lib/queries/garrison";

interface Props {
  playerId: string;
  /** Будівля, що тренує ці війська (казарма, стрільбище, двір списоносців). */
  buildingType: string;
}

/**
 * Скільки війська цього типу вдома, з розбивкою за рівнями (рішення 19.10.2026):
 * «Військо» пішло з нижньої панелі, тож кількість видно просто в будівлі, що його тренує.
 */
export default function TroopsPanel({ playerId, buildingType }: Props) {
  const catalog = useCatalog();
  const garrison = useGarrison(playerId);

  const types = catalog.units.filter((unit) => unit.requiresBuilding === buildingType);
  if (types.length === 0) return null;

  const units = garrison.data?.units ?? [];

  return (
    <div className="space-y-1 rounded-lg bg-slate-50 p-2 text-sm">
      {types.map((type) => {
        const stacks = units.filter((unit) => unit.unitType === type.key && unit.count > 0).sort((a, b) => a.level - b.level);
        const total = stacks.reduce((sum, unit) => sum + unit.count, 0);

        return (
          <div key={type.key} className="flex flex-wrap items-baseline justify-between gap-x-2">
            <span className="font-medium text-slate-700">{catalog.unitName(type.key)}</span>
            <span className="text-slate-800">
              <span className="font-semibold">{total.toLocaleString("uk-UA")}</span>
              {stacks.length > 1 && (
                <span className="ml-1 text-xs text-slate-500">
                  ({stacks.map((unit) => `${unit.level} рів.: ${unit.count.toLocaleString("uk-UA")}`).join(", ")})
                </span>
              )}
              {stacks.length === 1 && <span className="ml-1 text-xs text-slate-500">· {stacks[0]?.level} рів.</span>}
            </span>
          </div>
        );
      })}
    </div>
  );
}
