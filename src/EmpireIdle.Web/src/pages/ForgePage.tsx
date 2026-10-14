import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { EquipmentFeed, EquipmentMastery } from "../components/forge/EquipmentUpgrade";
import ItemIcon from "../components/inventory/ItemIcon";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { useInventory } from "../lib/queries/inventory";
import { rarityKey, rarityLabel, rarityStyle } from "../lib/rarity";
import { statLabel, statValue } from "../lib/statNames";

const RARITY_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

interface RowProps {
  equipment: EquipmentResponse;
  wearer: string | null;
  selected: boolean;
  onSelect: () => void;
}

function EquipmentRow({ equipment, wearer, selected, onSelect }: RowProps) {
  const catalog = useCatalog();

  return (
    <button
      type="button"
      onClick={onSelect}
      className={`flex w-full items-center gap-3 rounded-xl border p-2 text-left transition ${
        selected ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white hover:border-slate-300"
      }`}
    >
      <ItemIcon itemKey={equipment.itemKey} type="equipment" rarity={equipment.rarity} mastery={equipment.mastery} size={40} />
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
 * і гаєчки: згодований передає весь свій досвід. Майстерність коваля — спроба за камені для заточування з шансом;
 * на пізніх заточках невдача може зламати предмет, і тоді його ремонтують за gems чи ремкомплектом.
 * Повна картка предмета з «до → після» — окремим кроком.
 */
export default function ForgePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);

  // Картка спорядження веде сюди з ?item=<id>: кузня відкривається саме на цьому предметі
  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedId, setSelectedId] = useState<string | null>(() => searchParams.get("item"));

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

  const selected = equipment.find((item) => item.id === selectedId) ?? equipment[0] ?? null;
  const heroName = (heroId: string | null | undefined) => {
    const hero = heroes.data?.heroes.find((candidate) => candidate.id === heroId);
    return hero === undefined ? null : catalog.heroName(hero.heroKey);
  };

  const select = (id: string) => {
    setSelectedId(id);
    setSearchParams({ item: id }, { replace: true });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Кузня</h1>
      </div>

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
                selected={selected?.id === item.id}
                onSelect={() => select(item.id)}
              />
            ))}
          </div>

          {selected !== null && (
            <aside className="h-fit space-y-4 rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex gap-3">
                <ItemIcon itemKey={selected.itemKey} type="equipment" rarity={selected.rarity} mastery={selected.mastery} size={72} />
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
                    <dt className="text-xs text-slate-500">{statLabel(stat)}</dt>
                    <dd className="font-medium text-slate-800">{statValue(stat, value)}</dd>
                  </div>
                ))}
              </dl>

              {/* key — новий предмет починає з чистого вибору */}
              <EquipmentFeed key={selected.id} playerId={playerId} selected={selected} />
              <EquipmentMastery playerId={playerId} selected={selected} />
            </aside>
          )}
        </div>
      )}
    </div>
  );
}
