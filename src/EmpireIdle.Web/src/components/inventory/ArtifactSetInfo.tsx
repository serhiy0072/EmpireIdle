import { Link } from "react-router-dom";
import { useCatalog } from "../../lib/queries/catalog";
import { statLabel, statValue } from "../../lib/statNames";

interface Props {
  itemKey: string;
  /** Ключі предметів, вдягнених на того самого героя; порожньо — предмет ні на кому. */
  wornKeys: string[];
}

/**
 * Набір артефакта на картці предмета: скільки частин цієї рідкості вдягнено на героя,
 * бонус набору — зеленим, коли діє, сірим, коли ні, — і данж, де набір падає.
 */
export default function ArtifactSetInfo({ itemKey, wornKeys }: Props) {
  const catalog = useCatalog();
  const set = catalog.setOfItem(itemKey);
  // Бонус рахується по частинах однієї рідкості — як у HeroStats.SetBonus на сервері
  const group = set?.rarities.find((rarity) => rarity.pieceKeys.includes(itemKey));

  if (set === null || group === undefined) return null;

  const worn = group.pieceKeys.filter((key) => wornKeys.includes(key)).length;
  const active = worn >= group.requiredPieces;
  const bonus = Object.entries(group.bonus);

  return (
    <section className="space-y-1">
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold">
          <Link to={`/inventory/sets?set=${set.key}`} className="hover:underline">
            {set.displayName}
          </Link>
        </h3>
        <span className={`text-xs font-semibold tabular-nums ${active ? "text-emerald-700" : "text-slate-500"}`}>
          {worn}/{group.pieceKeys.length}
        </span>
      </div>

      <div
        className={`rounded-lg border px-3 py-1.5 text-sm ${
          active ? "border-emerald-300 bg-emerald-50 text-emerald-800" : "border-slate-200 bg-slate-50 text-slate-400"
        }`}
      >
        <p className="text-xs font-medium">
          {group.requiredPieces} частини{active ? " · діє" : ""}
        </p>
        {bonus.length === 0 ? (
          <p>—</p>
        ) : (
          <ul>
            {bonus.map(([stat, value]) => (
              <li key={stat} className="flex justify-between gap-2">
                <span>{statLabel(stat)}</span>
                <span className="font-semibold tabular-nums">+{statValue(stat, value)}</span>
              </li>
            ))}
          </ul>
        )}
      </div>

      {set.dungeon != null && (
        <p className="text-xs text-slate-500">
          Де отримати:{" "}
          <Link to={`/dungeons?dungeon=${set.dungeon.key}`} className="font-medium text-emerald-700 hover:underline">
            {set.dungeon.displayName}
          </Link>{" "}
          · ратуша {set.dungeon.requiresMainBuildingLevel}
        </p>
      )}
    </section>
  );
}
