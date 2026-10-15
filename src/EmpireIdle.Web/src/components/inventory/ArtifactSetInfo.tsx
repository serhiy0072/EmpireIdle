import { Link } from "react-router-dom";
import { classLabel } from "../../lib/heroClasses";
import { useCatalog } from "../../lib/queries/catalog";
import { rarityLabel } from "../../lib/rarity";
import { setThresholds, wornOfSet } from "../../lib/setBonus";
import { bonusValue, isDungeonOnly, statHint, statLabelForClass } from "../../lib/statNames";
import { RoleBadge } from "../game/GameUi";

interface Props {
  itemKey: string;
  /** Ключі предметів, вдягнених на того самого героя; порожньо — предмет ні на кому. */
  wornKeys: string[];
}

/**
 * Набір артефакта на картці предмета (рішення 15.10.2026): клас, що його вдягає, скільки предметів родини
 * на герої і два пороги — 2 і 4 предмети. Досягнутий поріг зелений, недосягнутий сірий; числа — з рідкості,
 * що діє на порозі, а для недосягнутого — з рідкості цього предмета. Внизу — данж, де набір падає.
 */
export default function ArtifactSetInfo({ itemKey, wornKeys }: Props) {
  const catalog = useCatalog();
  const set = catalog.setOfItem(itemKey);
  const own = set?.rarities.find((rarity) => rarity.pieceKeys.includes(itemKey));

  if (set === null || own === undefined) return null;

  const worn = wornOfSet(set, wornKeys);
  const thresholds = setThresholds(set, wornKeys, own.rarity);
  const slots = Math.max(...thresholds.map((threshold) => threshold.required), own.pieceKeys.length);

  return (
    <section className="space-y-1">
      <div className="flex items-center justify-between gap-2">
        <h3 className="flex items-center gap-1.5 text-sm font-semibold">
          {set.class != null && <RoleBadge role={set.class} size={16} />}
          <Link to={`/inventory/sets?set=${set.key}`} className="hover:underline">
            {set.displayName}
          </Link>
        </h3>
        <span className={`text-xs font-semibold tabular-nums ${worn >= 2 ? "text-emerald-700" : "text-slate-500"}`}>
          {worn}/{slots} вдягнено
        </span>
      </div>

      {set.class != null && <p className="text-xs text-slate-500">Лише для класу «{classLabel(set.class)}»</p>}

      {thresholds.map((threshold) => {
        const bonus = Object.entries(threshold.bonus);

        return (
          <div
            key={threshold.required}
            className={`rounded-lg border px-3 py-1.5 text-sm ${
              threshold.active ? "border-emerald-300 bg-emerald-50 text-emerald-800" : "border-slate-200 bg-slate-50 text-slate-400"
            }`}
          >
            <p className="text-xs font-medium">
              {threshold.required}/{slots}
              {threshold.active ? ` · діє · ${rarityLabel(threshold.rarity).toLowerCase()}` : ""}
            </p>
            {bonus.length === 0 ? (
              <p>—</p>
            ) : (
              <ul>
                {bonus.map(([stat, value]) => (
                  <li key={stat} className="flex justify-between gap-2" title={statHint(stat)}>
                    <span className={statHint(stat) === undefined ? "" : "cursor-help underline decoration-dotted"}>
                      {statLabelForClass(stat, set.class)}
                      {isDungeonOnly(stat) && <span className="text-xs opacity-75"> · у данжі</span>}
                    </span>
                    <span className="font-semibold tabular-nums">{bonusValue(stat, value)}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        );
      })}

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
