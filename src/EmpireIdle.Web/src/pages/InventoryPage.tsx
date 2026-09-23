import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import ActiveEffects from "../components/inventory/ActiveEffects";
import EquipmentCard from "../components/inventory/EquipmentCard";
import ItemCard from "../components/inventory/ItemCard";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse, InventoryItemResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { rarityKey } from "../lib/rarity";
import { useEquip, useInventory, useUnequip, useUseItem } from "../lib/queries/inventory";

type SortKey = "rarity" | "level" | "type";

const SORT_LABELS: Record<SortKey, string> = { rarity: "за рідкістю", level: "за рівнем", type: "за типом" };

const RARITY_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

/** Стабільний порядок: обране поле, далі рідкість, далі назва — щоб однакові мечі не стрибали. */
function sortEquipment(items: EquipmentResponse[], sort: SortKey, name: (key: string) => string): EquipmentResponse[] {
  const byRarity = (a: EquipmentResponse, b: EquipmentResponse) =>
    (RARITY_ORDER[rarityKey(a.rarity)] ?? 9) - (RARITY_ORDER[rarityKey(b.rarity)] ?? 9);
  const byLevel = (a: EquipmentResponse, b: EquipmentResponse) => b.enhancementLevel - a.enhancementLevel;
  const byType = (a: EquipmentResponse, b: EquipmentResponse) => a.slot.localeCompare(b.slot) || name(a.itemKey).localeCompare(name(b.itemKey), "uk");
  const primary = sort === "rarity" ? byRarity : sort === "level" ? byLevel : byType;

  // Одягнуте — завжди вгорі: це те, що гравець шукає найчастіше
  return [...items].sort(
    (a, b) =>
      Number(b.equippedByHeroId != null) - Number(a.equippedByHeroId != null) ||
      primary(a, b) ||
      byRarity(a, b) ||
      byLevel(a, b) ||
      byType(a, b),
  );
}

function sortItems(items: InventoryItemResponse[], sort: SortKey): InventoryItemResponse[] {
  const byRarity = (a: InventoryItemResponse, b: InventoryItemResponse) =>
    (RARITY_ORDER[rarityKey(a.rarity)] ?? 9) - (RARITY_ORDER[rarityKey(b.rarity)] ?? 9);
  const byType = (a: InventoryItemResponse, b: InventoryItemResponse) => a.type.localeCompare(b.type) || a.displayName.localeCompare(b.displayName, "uk");
  // «За рівнем» для стаків — за кількістю: рівня в них немає
  const byCount = (a: InventoryItemResponse, b: InventoryItemResponse) => b.count - a.count;
  const primary = sort === "rarity" ? byRarity : sort === "level" ? byCount : byType;

  return [...items].sort((a, b) => primary(a, b) || byRarity(a, b) || byType(a, b));
}

export default function InventoryPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);

  const consume = useUseItem(playerId);
  const equip = useEquip(playerId);
  const unequip = useUnequip(playerId);

  const [tab, setTab] = useState<"items" | "equipment">("equipment");
  const [sort, setSort] = useState<SortKey>("rarity");

  const equipment = useMemo(
    () => sortEquipment(inventory.data?.equipment ?? [], sort, catalog.itemName),
    [inventory.data?.equipment, sort, catalog.itemName],
  );
  const items = useMemo(() => sortItems(inventory.data?.items ?? [], sort), [inventory.data?.items, sort]);

  if (inventory.isPending) {
    return <p className="text-slate-500">Завантаження інвентаря…</p>;
  }

  if (inventory.isError) {
    return <ErrorBanner error={inventory.error} />;
  }

  const busy = consume.isPending || equip.isPending || unequip.isPending;
  const failure = consume.error ?? equip.error ?? unequip.error;

  const tabs = [
    { key: "equipment", label: `Спорядження · ${inventory.data.equipment.length}` },
    { key: "items", label: `Предмети · ${inventory.data.items.length}` },
  ] as const;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Інвентар</h1>
        <div className="flex flex-wrap items-baseline gap-3">
          <Link to="/inventory/sets" className="text-sm text-emerald-700 hover:underline">
            набори артефактів
          </Link>
          <ActiveEffects effects={inventory.data.activeEffects} now={now} />
        </div>
      </div>

      <ErrorBanner error={failure} />

      <div className="flex flex-wrap items-center gap-1">
        <nav className="flex gap-1">
          {tabs.map((item) => (
            <button
              key={item.key}
              type="button"
              onClick={() => setTab(item.key)}
              className={`rounded-lg px-3 py-1 text-sm ${
                tab === item.key ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"
              }`}
            >
              {item.label}
            </button>
          ))}
        </nav>

        <select
          value={sort}
          onChange={(event) => setSort(event.target.value as SortKey)}
          aria-label="Сортування"
          className="ml-auto rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700"
        >
          {(Object.keys(SORT_LABELS) as SortKey[]).map((key) => (
            <option key={key} value={key}>
              {SORT_LABELS[key]}
            </option>
          ))}
        </select>
      </div>

      {tab === "equipment" &&
        (inventory.data.equipment.length === 0 ? (
          <p className="text-sm text-slate-500">Спорядження ще немає — купіть зброю в кузні або крутіть банери.</p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {equipment.map((equipment) => (
              <EquipmentCard
                key={equipment.id}
                equipment={equipment}
                heroes={heroes.data?.heroes ?? []}
                busy={busy}
                onEquip={(heroId) => equip.mutate({ heroId, equipmentId: equipment.id })}
                onUnequip={() => unequip.mutate(equipment.id)}
              />
            ))}
          </div>
        ))}

      {tab === "items" &&
        (inventory.data.items.length === 0 ? (
          <p className="text-sm text-slate-500">Порожньо. Предмети дають квести й банери.</p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {items.map((item) => (
              <ItemCard key={item.itemKey} item={item} busy={busy} onUse={(request) => consume.mutate(request)} />
            ))}
          </div>
        ))}
    </div>
  );
}
