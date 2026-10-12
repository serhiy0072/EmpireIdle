import { useMemo, useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import ItemIcon from "../components/inventory/ItemIcon";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { useFeedArtifact, useInventory, useRaiseMastery, useRepairArtifact } from "../lib/queries/inventory";
import { useWallet } from "../lib/queries/wallet";
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
          {equipment.isBroken && <span className="rounded bg-rose-100 px-1.5 text-rose-700">зламано</span>}
          {wearer !== null && <span className="text-slate-500">{wearer}</span>}
        </div>
      </div>
    </button>
  );
}

/**
 * Кузня: два виміри артефакта (GDD §6.4). Посилення — згодувати інше спорядження (крім унікального)
 * і гаєчки: згодований передає весь свій досвід. Майстерність коваля — спроба за золото з шансом;
 * на пізніх заточках невдача може зламати предмет, і тоді його ремонтують за gems чи ремкомплектом.
 * Повна картка предмета з «до → після» — окремим кроком.
 */
export default function ForgePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);

  const feed = useFeedArtifact(playerId);
  const mastery = useRaiseMastery(playerId);
  const repair = useRepairArtifact(playerId);
  const wallet = useWallet(playerId);

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

  const busy = feed.isPending || mastery.isPending || repair.isPending;
  const failure = feed.error ?? mastery.error ?? repair.error;

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

  // Ремонт: gems за кожен рівень заточки або один ремкомплект (GDD §9.12)
  const repairGems = (selected?.mastery ?? 0) * catalog.artifactRepair.gemsPerMastery;
  const kits = inventory.data.items.find((item) => item.itemKey === catalog.artifactRepair.kitItemKey)?.count ?? 0;
  // Спроба на +n має шанс поломки з індексом n − 1: зараз це наступна заточка
  const breakChance = catalog.artifactRepair.breakChances[selected?.mastery ?? 0] ?? 0;
  const attempt = catalog.masteryAttempts[selected?.mastery ?? 0] ?? null;

  // Рівень після обраного — та сама крива, що на сервері
  const levelAfter = (() => {
    if (selected === null) return 0;
    const curve = catalog.artifactLevelExperience;
    let level = selected.level;
    while (level + 1 < curve.length && (curve[level + 1] ?? Infinity) <= selected.experience + gained) level++;
    return level;
  })();

  /**
   * Автовибір: кожне натискання докладає «їжі» рівно до ще одного рівня понад уже обране. Спершу
   * непрокачані предмети від найдешевших — щоб не перебрати й не з'їсти вкладене, — потім гаєчки.
   */
  const autoPick = () => {
    if (selected === null) return;

    const target = catalog.artifactLevelExperience[levelAfter + 1];
    if (target === undefined) return;

    let missing = target - (selected.experience + gained);
    const nextFood = [...foodIds];
    const nextWrenches = { ...wrenches };

    const plain = food
      .filter((item) => !foodIds.includes(item.id) && item.level === 0 && item.mastery === 0)
      .sort((a, b) => (a.feedValue ?? 0) - (b.feedValue ?? 0));

    for (const item of plain) {
      if (missing <= 0) break;
      nextFood.push(item.id);
      missing -= item.feedValue ?? 0;
    }

    const byValue = [...ownedWrenches].sort((a, b) => wrenchExperience(a.itemKey) - wrenchExperience(b.itemKey));

    for (const wrench of byValue) {
      const value = wrenchExperience(wrench.itemKey);
      const used = nextWrenches[wrench.itemKey] ?? 0;
      if (missing <= 0 || value <= 0 || used >= wrench.count) continue;

      const take = Math.min(wrench.count - used, Math.ceil(missing / value));
      nextWrenches[wrench.itemKey] = used + take;
      missing -= take * value;
    }

    setFoodIds(nextFood);
    setWrenches(nextWrenches);
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
                      {gained > 0 && (
                        <span className="text-emerald-700">
                          {" "}
                          · обрано +{gained} → рівень {levelAfter}
                        </span>
                      )}
                    </p>

                    {food.length === 0 && ownedWrenches.length === 0 ? (
                      <p className="text-xs text-slate-500">Нема чим посилити: потрібні вільні неунікальні артефакти або гаєчки.</p>
                    ) : (
                      <div className="flex gap-2">
                        <button
                          type="button"
                          onClick={autoPick}
                          disabled={busy}
                          className="rounded-lg border border-amber-400 px-3 py-1 text-sm font-medium text-amber-800 hover:bg-amber-50 disabled:opacity-50"
                        >
                          Автовибір +1 рівень
                        </button>
                        {gained > 0 && (
                          <button type="button" onClick={reset} disabled={busy} className="text-sm text-slate-500 hover:underline">
                            очистити
                          </button>
                        )}
                      </div>
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
                    {mastery.data.success
                      ? "Вдалося!"
                      : mastery.data.broken
                        ? "Не вдалося — предмет зламався. Відремонтуйте його, щоб повернути повні стати й точити далі."
                        : "Не вдалося — золото витрачено, предмет цілий."}
                  </p>
                )}
                {selected.isBroken ? (
                  <div className="space-y-2 rounded-lg border border-rose-200 bg-rose-50 p-2">
                    <p className="text-xs text-rose-800">
                      Зламаний: дає половину статів і не точиться. Рівень качати можна.
                    </p>
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        onClick={() => repair.mutate({ equipmentId: selected.id, useKit: false })}
                        disabled={busy || (wallet.data?.gemBalance ?? 0) < repairGems}
                        className="rounded-lg bg-violet-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50"
                      >
                        Відремонтувати за {repairGems.toLocaleString("uk-UA")} 💎
                      </button>
                      <button
                        type="button"
                        onClick={() => repair.mutate({ equipmentId: selected.id, useKit: true })}
                        disabled={busy || kits === 0}
                        className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        {catalog.itemName(catalog.artifactRepair.kitItemKey)} · є {kits}
                      </button>
                    </div>
                  </div>
                ) : selected.mastery < catalog.maxMastery ? (
                  <>
                    {breakChance > 0 && (
                      <p className="text-xs text-amber-700">
                        Обережно: після невдалої спроби предмет може зламатися ({Math.round(breakChance * 100)}%). Зламаний дає
                        половину статів, доки його не відремонтуєте.
                      </p>
                    )}
                    <button
                      type="button"
                      onClick={() => mastery.mutate(selected.id)}
                      disabled={busy}
                      className="rounded-lg bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-700 disabled:opacity-50"
                    >
                      {attempt === null
                        ? "Спробувати за золото"
                        : `Спробувати за ${attempt.gold.toLocaleString("uk-UA")} 🪙 · шанс ${Math.round(attempt.chance * 100)}%`}
                    </button>
                    {attempt !== null && (
                      <p className="text-xs text-slate-500">Невдача з'їдає золото, майстерність не падає.</p>
                    )}
                  </>
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
