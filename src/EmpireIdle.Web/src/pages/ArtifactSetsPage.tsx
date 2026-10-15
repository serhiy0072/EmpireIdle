import { useEffect, useMemo, useRef } from "react";
import { Link, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import ItemIcon from "../components/inventory/ItemIcon";
import { useSession } from "../hooks/useSession";
import { useCatalog, type CatalogArtifactSet } from "../lib/queries/catalog";
import { useInventory } from "../lib/queries/inventory";
import { RoleBadge } from "../components/game/GameUi";
import { classLabel } from "../lib/heroClasses";
import { rarityLabel, rarityStyle } from "../lib/rarity";
import { bonusValue, isDungeonOnly, statLabel, statLabelForClass } from "../lib/statNames";

/** Характер набору словами: гравцеві «Attack, Health» нічого не каже про роль. */
function character(set: CatalogArtifactSet): { label: string; style: string } | null {
  if (set.focusStats.includes("Attack")) return { label: "Атакувальний", style: "bg-rose-100 text-rose-800" };
  if (set.focusStats.includes("Defense")) return { label: "Захисний", style: "bg-sky-100 text-sky-800" };

  return set.focusStats.length > 0
    ? { label: set.focusStats.map(statLabel).join(", "), style: "bg-slate-100 text-slate-700" }
    : null;
}

function bonusText(bonus: Record<string, number>, heroClass: string | null | undefined): string {
  const lines = Object.entries(bonus).map(
    ([stat, value]) => `${statLabelForClass(stat, heroClass)} ${bonusValue(stat, value)}${isDungeonOnly(stat) ? " (у данжі)" : ""}`,
  );

  return lines.length === 0 ? "—" : lines.join(" · ");
}

/**
 * Кодекс наборів: усі родини артефактів за рівнями данжів. Гравець бачить, для якого класу набір,
 * з якого данжу він падає, що дають пороги 2 і 4 предмети і скільки частин уже зібрано.
 * ?set=ключ підсвічує й прокручує до потрібної родини — туди ведуть переходи
 * з вітрини данжу й картки артефакту.
 */
export default function ArtifactSetsPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();
  const inventory = useInventory(playerId);

  const [params] = useSearchParams();
  const focused = params.get("set");
  const focusedRef = useRef<HTMLElement | null>(null);

  // Скільки екземплярів кожної частини має гравець — одягнені теж рахуються
  const owned = useMemo(() => {
    const counts = new Map<string, number>();

    for (const item of inventory.data?.equipment ?? []) counts.set(item.itemKey, (counts.get(item.itemKey) ?? 0) + 1);

    return counts;
  }, [inventory.data?.equipment]);

  const tiers = useMemo(() => {
    const byTier = new Map<number, CatalogArtifactSet[]>();

    for (const set of catalog.artifactSets) byTier.set(set.tier, [...(byTier.get(set.tier) ?? []), set]);

    return [...byTier.entries()].sort(([a], [b]) => a - b);
  }, [catalog.artifactSets]);

  useEffect(() => {
    focusedRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
  }, [focused, catalog.loaded]);

  if (!catalog.loaded) {
    return <p className="text-slate-500">Завантаження наборів…</p>;
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Набори артефактів</h1>
        <p className="text-sm text-slate-500">
          <Link to="/inventory" className="text-emerald-700 hover:underline">
            інвентар
          </Link>{" "}
          ·{" "}
          <Link to="/dungeons" className="text-emerald-700 hover:underline">
            данжі
          </Link>
        </p>
      </div>

      <p className="text-sm text-slate-600">
        Кожен данж дає набір із чотирьох частин для одного класу героїв — інший клас його не вдягне. Два предмети
        набору дають перший бонус, чотири — ще один. Рідкість предметів можна змішувати: числа порогу беруться з
        рідкості N-го найрідкіснішого предмета, тож 2 унікальні й 2 звичайні дають перший бонус унікальним, а другий —
        звичайним.
      </p>

      {inventory.isError && <ErrorBanner error={inventory.error} />}

      {tiers.map(([tier, sets]) => (
        <section key={tier} className="space-y-2">
          <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">
            Рівень {tier} · стати ×{sets[0]?.statMultiplier.toLocaleString("uk-UA")}
          </h2>

          <div className="grid gap-3 md:grid-cols-2">
            {sets.map((set) => {
              const role = character(set);
              const isFocused = set.key === focused;

              return (
                <article
                  key={set.key}
                  ref={isFocused ? focusedRef : undefined}
                  className={`space-y-3 rounded-xl border bg-white p-4 ${
                    isFocused ? "border-emerald-500 ring-2 ring-emerald-200" : "border-slate-200"
                  }`}
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <h3 className="flex items-center gap-1.5 font-medium text-slate-800">
                      {set.class != null && <RoleBadge role={set.class} size={18} />}
                      {set.displayName}
                    </h3>
                    {role !== null && <span className={`rounded px-2 py-0.5 text-xs ${role.style}`}>{role.label}</span>}
                  </div>

                  {set.class != null && <p className="text-xs text-slate-500">Для класу «{classLabel(set.class)}»</p>}
                  {set.description !== "" && <p className="text-sm italic text-slate-600">{set.description}</p>}

                  {set.dungeon != null ? (
                    <p className="text-xs text-slate-500">
                      Падає в данжі{" "}
                      <Link to="/dungeons" className="text-emerald-700 hover:underline">
                        {set.dungeon.displayName}
                      </Link>{" "}
                      · ратуша {set.dungeon.requiresMainBuildingLevel}
                    </p>
                  ) : (
                    <p className="text-xs text-slate-500">Не падає в данжах</p>
                  )}

                  {set.focusStats.length > 0 && (
                    <p className="text-xs text-slate-500">
                      Характер набору: {set.focusStats.map(statLabel).join(", ").toLowerCase()}
                    </p>
                  )}

                  <ul className="space-y-2">
                    {set.rarities.map((rarity) => {
                      const collected = rarity.pieceKeys.filter((key) => (owned.get(key) ?? 0) > 0).length;
                      const complete = rarity.pieceKeys.length > 0 && collected >= rarity.pieceKeys.length;

                      return (
                        <li key={rarity.setKey} className="rounded-lg bg-slate-50 p-2">
                          <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
                            <span className={`rounded px-2 py-0.5 text-xs ${rarityStyle(rarity.rarity)}`}>
                              {rarityLabel(rarity.rarity)}
                            </span>
                            <span className={complete ? "text-xs font-medium text-emerald-700" : "text-xs text-slate-500"}>
                              зібрано {collected} / {rarity.pieceKeys.length}
                            </span>
                          </div>

                          {rarity.thresholds.map((threshold) => (
                            <p key={threshold.requiredPieces} className="mt-1 text-sm text-slate-700">
                              {threshold.requiredPieces} предмети: {bonusText(threshold.bonus, set.class)}
                            </p>
                          ))}

                          <div className="mt-2 flex flex-wrap gap-1">
                            {rarity.pieceKeys.map((key) => {
                              const has = (owned.get(key) ?? 0) > 0;

                              return (
                                <span
                                  key={key}
                                  title={catalog.itemName(key)}
                                  className={has ? "" : "opacity-30 grayscale"}
                                >
                                  <ItemIcon itemKey={key} type="equipment" rarity={rarity.rarity} size={32} />
                                </span>
                              );
                            })}
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                </article>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}
