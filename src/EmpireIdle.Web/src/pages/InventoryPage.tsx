import { useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import ActiveEffects from "../components/inventory/ActiveEffects";
import EquipmentCard from "../components/inventory/EquipmentCard";
import ItemCard from "../components/inventory/ItemCard";
import WeaponShop from "../components/inventory/WeaponShop";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import {
  useBuyWeapon,
  useEnhance,
  useEquip,
  useInventory,
  useRepair,
  useUnequip,
  useUpgradeArtifact,
  useUseItem,
} from "../lib/queries/inventory";

const OUTCOME_LABELS: Record<string, string> = {
  success: "Заточка вдалася",
  failure: "Заточка не вдалася — рівень не змінився",
  broken: "Зброя зламалася — потрібен ремонт",
};

export default function InventoryPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);

  const consume = useUseItem(playerId);
  const enhance = useEnhance(playerId);
  const repair = useRepair(playerId);
  const upgrade = useUpgradeArtifact(playerId);
  const buy = useBuyWeapon(playerId);
  const equip = useEquip(playerId);
  const unequip = useUnequip(playerId);

  const [tab, setTab] = useState<"items" | "equipment" | "shop">("equipment");

  if (inventory.isPending) {
    return <p className="text-slate-500">Завантаження інвентаря…</p>;
  }

  if (inventory.isError) {
    return <ErrorBanner error={inventory.error} />;
  }

  const busy =
    consume.isPending ||
    enhance.isPending ||
    repair.isPending ||
    upgrade.isPending ||
    buy.isPending ||
    equip.isPending ||
    unequip.isPending;

  const failure =
    consume.error ?? enhance.error ?? repair.error ?? upgrade.error ?? buy.error ?? equip.error ?? unequip.error;

  // Останній результат заточки видно, поки гравець не натисне щось інше
  const outcome = enhance.data?.outcome ?? null;

  const tabs = [
    { key: "equipment", label: `Спорядження · ${inventory.data.equipment.length}` },
    { key: "items", label: `Предмети · ${inventory.data.items.length}` },
    { key: "shop", label: "Кузня" },
  ] as const;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Інвентар</h1>
        <ActiveEffects effects={inventory.data.activeEffects} now={now} />
      </div>

      <ErrorBanner error={failure} />

      {outcome !== null && !enhance.isPending && (
        <div
          role="status"
          className={`rounded-lg px-3 py-2 text-sm ${
            outcome === "success" ? "bg-emerald-50 text-emerald-800" : "bg-amber-50 text-amber-900"
          }`}
        >
          {OUTCOME_LABELS[outcome] ?? outcome}
        </div>
      )}

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

      {tab === "equipment" &&
        (inventory.data.equipment.length === 0 ? (
          <p className="text-sm text-slate-500">Спорядження ще немає — купіть зброю в кузні або крутіть банери.</p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {inventory.data.equipment.map((equipment) => (
              <EquipmentCard
                key={equipment.id}
                equipment={equipment}
                heroes={heroes.data?.heroes ?? []}
                artifactSlots={catalog.artifactSlots}
                maxEnhancement={catalog.maxEnhancement}
                busy={busy}
                onEquip={(heroId, slotIndex) => equip.mutate({ heroId, equipmentId: equipment.id, slotIndex })}
                onUnequip={() => unequip.mutate(equipment.id)}
                onEnhance={() => enhance.mutate(equipment.id)}
                onRepair={() => repair.mutate(equipment.id)}
                onUpgrade={() => upgrade.mutate(equipment.id)}
              />
            ))}
          </div>
        ))}

      {tab === "items" &&
        (inventory.data.items.length === 0 ? (
          <p className="text-sm text-slate-500">Порожньо. Предмети дають квести й банери.</p>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {inventory.data.items.map((item) => (
              <ItemCard key={item.itemKey} item={item} busy={busy} onUse={(request) => consume.mutate(request)} />
            ))}
          </div>
        ))}

      {tab === "shop" && <WeaponShop busy={busy} onBuy={(itemKey) => buy.mutate(itemKey)} />}
    </div>
  );
}
