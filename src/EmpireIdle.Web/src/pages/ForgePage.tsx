import { useMemo, useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import ItemIcon from "../components/inventory/ItemIcon";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { useInventory, useUpgradeArtifact } from "../lib/queries/inventory";
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
  maxEnhancement: number;
  busy: boolean;
  selected: boolean;
  onSelect: () => void;
}

function EquipmentRow({ equipment, wearer, maxEnhancement, busy, selected, onSelect }: RowProps) {
  const catalog = useCatalog();
  const atCap = equipment.enhancementLevel >= maxEnhancement;

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
        <div className="truncate text-sm font-medium text-slate-800">
          {catalog.itemName(equipment.itemKey)}
          {equipment.enhancementLevel > 0 && <span className="ml-1 text-amber-600">+{equipment.enhancementLevel}</span>}
        </div>
        <div className="flex flex-wrap gap-1 text-xs">
          <span className={`rounded px-1.5 ${rarityStyle(equipment.rarity)}`}>{rarityLabel(equipment.rarity)}</span>
          {wearer !== null && <span className="text-slate-500">{wearer}</span>}
          {atCap && <span className="text-slate-500">максимум</span>}
        </div>
      </div>
    </button>
  );
}

/**
 * Кузня: прокачка артефактів. Зброя — частина героя й качається на його екрані (GDD §6.4);
 * одягання лишається в рюкзаку й на екрані героя — тут працюють із самим предметом.
 */
export default function ForgePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);

  const upgrade = useUpgradeArtifact(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Унікальне й прокачане — вгорі: саме з ним приходять до кузні
  const equipment = useMemo(
    () =>
      [...(inventory.data?.equipment ?? [])].sort(
        (a, b) =>
          (RARITY_ORDER[rarityKey(a.rarity)] ?? 9) - (RARITY_ORDER[rarityKey(b.rarity)] ?? 9) ||
          b.enhancementLevel - a.enhancementLevel,
      ),
    [inventory.data?.equipment],
  );

  if (inventory.isPending) {
    return <p className="text-slate-500">Розпалюємо горно…</p>;
  }

  if (inventory.isError) {
    return <ErrorBanner error={inventory.error} />;
  }

  const busy = upgrade.isPending;
  const failure = upgrade.error;

  const selected = equipment.find((item) => item.id === selectedId) ?? equipment[0] ?? null;
  const heroName = (heroId: string | null | undefined) => {
    const hero = heroes.data?.heroes.find((candidate) => candidate.id === heroId);
    return hero === undefined ? null : catalog.heroName(hero.heroKey);
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
                  maxEnhancement={catalog.maxEnhancement}
                  busy={busy}
                  selected={selected?.id === item.id}
                  onSelect={() => setSelectedId(item.id)}
                />
              ))}
            </div>

            {selected !== null && (
              <aside className="h-fit space-y-4 rounded-xl border border-slate-200 bg-white p-4">
                <div className="flex gap-3">
                  <ItemIcon itemKey={selected.itemKey} type="equipment" rarity={selected.rarity} size={72} />
                  <div className="min-w-0">
                    <h2 className="text-lg font-medium text-slate-800">
                      {catalog.itemName(selected.itemKey)}
                      {selected.enhancementLevel > 0 && <span className="ml-1 text-amber-600">+{selected.enhancementLevel}</span>}
                    </h2>
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

                <p className="text-xs text-slate-500">
                  Рівень {selected.enhancementLevel} з {catalog.maxEnhancement}
                  {" · прокачка коштує золота села й завжди вдається"}
                </p>

                <div className="flex flex-wrap gap-2">
                  {selected.enhancementLevel < catalog.maxEnhancement && (
                    <button
                      type="button"
                      onClick={() => upgrade.mutate(selected.id)}
                      disabled={busy}
                      className="rounded-lg bg-amber-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
                    >
                      Покращити до +{selected.enhancementLevel + 1}
                    </button>
                  )}
                </div>
              </aside>
            )}
          </div>
        )}
    </div>
  );
}
