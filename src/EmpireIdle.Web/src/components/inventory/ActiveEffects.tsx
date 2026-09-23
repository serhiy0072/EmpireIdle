import type { ActiveEffectResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { formatRemaining } from "../../lib/time";

interface Props {
  effects: ActiveEffectResponse[];
  now: number;
}

const TARGET_LABELS: Record<string, string> = {
  Production: "виробіток",
  Attack: "атака",
  Defense: "захист",
};

/** Смужка діючих бустів із відліком до кінця. Без бустів нічого не малює. */
export default function ActiveEffects({ effects, now }: Props) {
  const catalog = useCatalog();

  if (effects.length === 0) return null;

  return (
    <div className="flex flex-wrap gap-2">
      {effects.map((effect) => (
        <span
          key={`${effect.target}:${effect.sourceItemKey}:${effect.expiresAt}`}
          className="rounded-full bg-violet-100 px-3 py-1 text-sm text-violet-800"
        >
          ×{effect.multiplier} {TARGET_LABELS[effect.target] ?? effect.target.toLowerCase()} ·{" "}
          {formatRemaining(effect.expiresAt, now)}
          <span className="ml-1 text-xs opacity-70">{catalog.itemName(effect.sourceItemKey)}</span>
        </span>
      ))}
    </div>
  );
}
