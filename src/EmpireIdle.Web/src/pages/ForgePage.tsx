import { useMemo, useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import ItemIcon from "../components/inventory/ItemIcon";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { useFeedArtifact, useInventory, useRaiseMastery } from "../lib/queries/inventory";
import { rarityKey, rarityLabel, rarityStyle } from "../lib/rarity";

const RARITY_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

const STAT_LABELS: Record<string, string> = {
  Attack: "Атака",
  Defense: "Захист",
  Health: "Здоров'я",
};

interface RowProps {
  equipment: EquipmentResponse;
  wearer: string | null;
  busy: boolean;
  selected: boolean;
  onSelect: () => void;
}

function EquipmentRow({ equipment, wearer, busy, selected, onSelect }: RowProps) {
  const catalog = useCatalog();

  return (
    <button
      type="button"
      onClick={onSelect}
      disabled={busy}
      className={`flex w-full items-center gap-3 rounded-xl border p-2 text-left transition ${
        selected ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white hover:border-slate-300"
      }`}
    >
      <ItemIcon itemKey={equipment.itemKey} type="equipment" rarity={equipment.rarity} size={40} />
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium text-slate-800">{catalog.itemName(equipment.itemKey)}</div>
        <div className="flex flex-wrap gap-1 text-xs">
          <span className={`rounded px-1.5 ${rarityStyle(equipment.rarity)}`}>{rarityLabel(equipment.rarity)}</span>
          <span className="text-slate-600">рів. {equipment.level}</span>
          {equipment.mastery > 0 && <span className="text-sky-700">майст. {equipment.mastery}</span>}
          {wearer !== null && <span className="text-slate-500">{wearer}</span>}
        </div>
      </div>
    </button>
  );
}

/**
 * Кузня: два виміри артефакта (GDD §6.4). Посилення — згодувати інше спорядження (крім унікального)
 * і гаєчки: згодований передає весь свій досвід. Майстерність коваля — спроба за золото з шансом,
 * без поломки. Повна картка предмета з «до → після» — окремим кроком.
 */
export default function ForgePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);

  const feed = useFeedArtifact(playerId);
  const mastery = useRaiseMastery(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [foodIds, setFoodIds] = useState<string[]>([]);
  const [wrenches, setWrenches] = useState<Record<string, number>>({});
  const [confirming, setConfirming] = useState(false);

  // Унікальне й прокачане — вгорі: саме з ним приходять до кузні
  const equipment = useMemo(
    () =>
      [...(inventory.data?.equipment ?? [])].sort(
        (a, b) =>
          (RARITY_ORDER[rarityKey(a.rarity)] ?? 9) - (RARITY_ORDER[rarityKey(b.rarity)] ?? 9) ||
          b.level - a.level ||
          b.mastery - a.mastery,
      ),
    [inventory.data?.equipment],
  );

  if (inventory.isPending) {
    return <p className="text-slate-500">Розпалюємо горно…</p>;
  }

  if (inventory.isError) {
    return <ErrorBanner error={inventory.error} />;
  }

  const busy = feed.isPending || mastery.isPending;
  const failure = feed.error ?? mastery.error;

  const selected = equipment.find((item) => item.id === selectedId) ?? equipment[0] ?? null;
  const heroName = (heroId: string | null | undefined) => {
    const hero = heroes.data?.heroes.find((candidate) => candidate.id === heroId);
    return hero === undefined ? null : catalog.heroName(hero.heroKey);
  };

  // Годуються лише вільні неунікальні: сервер відмовить решті, тож клієнт їх і не пропонує
  const food = equipment.filter(
    (item) =>
      item.id !== selected?.id && item.feedValue != null && item.equippedByHeroId == null && !item.isOnMarket,
  );
  const ownedWrenches = (inventory.data?.items ?? []).filter((item) => item.type === "wrench");

  const chosenFood = food.filter((item) => foodIds.includes(item.id));
  const wrenchExperience = (key: string) => catalog.item(key)?.artifactExperience ?? 0;
  const gained =
    chosenFood.reduce((sum, item) => sum + (item.feedValue ?? 0), 0) +
    Object.entries(wrenches).reduce((sum, [key, count]) => sum + wrenchExperience(key) * count, 0);
  // Попередження: прокачаний предмет зникне разом із вкладеним (він передасться, але сам предмет — ні)
  const levelledFood = chosenFood.filter((item) => item.level > 0 || item.mastery > 0);

  const reset = () => {
    setFoodIds([]);
    setWrenches({});
    setConfirming(false);
  };

  const submitFeed = () => {
    if (selected === null) return;
    feed.mutate({ equipmentId: selected.id, foodIds, wrenches }, { onSuccess: reset });
  };

  const select = (id: string) => {
    setSelectedId(id);
    reset();
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Кузня</h1>
      </div>

      <ErrorBanner error={failure} />

      {equipment.length === 0 ? (
        <p className="text-sm text-slate-500">Артефактів ще немає — вони падають у данжах.</p>
      ) : (
        <div className="grid gap-4 lg:grid-cols-[1fr_1fr]">
          <div className="max-h-[560px] space-y-2 overflow-y-auto pr-1">
            {equipment.map((item) => (
              <EquipmentRow
                key={item.id}
                equipment={item}
                wearer={heroName(item.equippedByHeroId)}
                busy={busy}
                selected={selected?.id === item.id}
                onSelect={() => select(item.id)}
              />
            ))}
          </div>

          {selected !== null && (
            <aside className="h-fit space-y-4 rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex gap-3">
                <ItemIcon itemKey={selected.itemKey} type="equipment" rarity={selected.rarity} size={72} />
                <div className="min-w-0">
                  <h2 className="text-lg font-medium text-slate-800">{catalog.itemName(selected.itemKey)}</h2>
                  <div className="mt-1 flex flex-wrap gap-1 text-xs">
                    <span className={`rounded px-2 py-0.5 ${rarityStyle(selected.rarity)}`}>{rarityLabel(selected.rarity)}</span>
                    <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{catalog.artifactSlotName(selected.itemKey) ?? "Артефакт"}</span>
                  </div>
                </div>
              </div>

              <dl className="grid grid-cols-3 gap-1 text-sm">
                {Object.entries(selected.stats).map(([stat, value]) => (
                  <div key={stat} className="rounded bg-slate-50 px-2 py-1">
                    <dt className="text-xs text-slate-500">{STAT_LABELS[stat] ?? stat}</dt>
                    <dd className="font-medium text-slate-800">{Math.round(value)}</dd>
                  </div>
                ))}
              </dl>

              <section className="space-y-2">
                <h3 className="text-sm font-medium text-slate-800">
                  Посилення · рівень {selected.level} з {catalog.maxArtifactLevel}
                </h3>

                {selected.experienceToNext == null ? (
                  <p className="text-xs text-slate-500">Найвищий рівень.</p>
                ) : (
                  <>
                    <p className="text-xs text-slate-500">
                      До наступного рівня — {selected.experienceToNext} досвіду
                      {gained > 0 && <span className="text-emerald-700"> · обрано +{gained}</span>}
                    </p>

                    {food.length === 0 && ownedWrenches.length === 0 && (
                      <p className="text-xs text-slate-500">Нема чим посилити: потрібні вільні неунікальні артефакти або гаєчки.</p>
                    )}

                    <div className="max-h-40 space-y-1 overflow-y-auto">
                      {food.map((item) => (
                        <label key={item.id} className="flex items-center gap-2 text-sm text-slate-700">
                          <input
                            type="checkbox"
                            checked={foodIds.includes(item.id)}
                            disabled={busy}
                            onChange={(event) =>
                              setFoodIds((previous) =>
                                event.target.checked ? [...previous, item.id] : previous.filter((id) => id !== item.id),
                              )
                            }
                          />
                          <span className="truncate">{catalog.itemName(item.itemKey)}</span>
                          {item.level > 0 && <span className="text-xs text-amber-700">рів. {item.level}</span>}
                          <span className="ml-auto text-xs text-slate-500">+{item.feedValue}</span>
                        </label>
                      ))}
                    </div>

                    {ownedWrenches.map((wrench) => (
                      <label key={wrench.itemKey} className="flex items-center gap-2 text-sm text-slate-700">
                        <span className="truncate">{wrench.displayName}</span>
                        <span className="text-xs text-slate-500">+{wrenchExperience(wrench.itemKey)} · є {wrench.count}</span>
                        <input
                          type="number"
                          min={0}
                          max={wrench.count}
                          value={wrenches[wrench.itemKey] ?? 0}
                          disabled={busy}
                          onChange={(event) => {
                            const count = Math.max(0, Math.min(wrench.count, Number(event.target.value) || 0));
                            setWrenches((previous) => {
                              const next = { ...previous };
                              if (count === 0) delete next[wrench.itemKey];
                              else next[wrench.itemKey] = count;
                              return next;
                            });
                          }}
                          className="ml-auto w-20 rounded border border-slate-300 px-2 py-0.5 text-right"
                        />
                      </label>
                    ))}

                    {confirming ? (
                      <div className="space-y-2 rounded-lg bg-amber-50 p-2 text-xs text-amber-900">
                        <p>
                          Серед обраного є прокачані предмети ({levelledFood.map((item) => catalog.itemName(item.itemKey)).join(", ")}).
                          Їхній досвід перейде в ціль, але самі предмети зникнуть разом із майстерністю.
                        </p>
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={submitFeed}
                            disabled={busy}
                            className="rounded-lg bg-amber-500 px-3 py-1 font-medium text-white hover:bg-amber-600 disabled:opacity-50"
                          >
                            Згодувати
                          </button>
                          <button
                            type="button"
                            onClick={() => setConfirming(false)}
                            className="rounded-lg border border-amber-300 px-3 py-1 hover:bg-amber-100"
                          >
                            Скасувати
                          </button>
                        </div>
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() => (levelledFood.length > 0 ? setConfirming(true) : submitFeed())}
                        disabled={busy || gained === 0}
                        className="rounded-lg bg-amber-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
                      >
                        Посилити
                      </button>
                    )}
                  </>
                )}
              </section>

              <section className="space-y-2">
                <h3 className="text-sm font-medium text-slate-800">
                  Майстерність коваля · {selected.mastery} з {catalog.maxMastery}
                </h3>
                {mastery.data !== undefined && mastery.variables === selected.id && (
                  <p className={`text-xs ${mastery.data.success ? "text-emerald-700" : "text-rose-700"}`}>
                    {mastery.data.success ? "Вдалося!" : "Не вдалося — золото витрачено, предмет цілий."}
                  </p>
                )}
                {selected.mastery < catalog.maxMastery ? (
                  <button
                    type="button"
                    onClick={() => mastery.mutate(selected.id)}
                    disabled={busy}
                    className="rounded-lg bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
                  >
                    Спробувати за золото
                  </button>
                ) : (
                  <p className="text-xs text-slate-500">Найвища майстерність.</p>
                )}
              </section>
            </aside>
          )}
        </div>
      )}
    </div>
  );
}
