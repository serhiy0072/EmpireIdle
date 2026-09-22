import { useMemo, useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import ItemIcon from "../components/inventory/ItemIcon";
import WeaponShop from "../components/inventory/WeaponShop";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { useBuyWeapon, useEnhance, useInventory, useRepair, useUpgradeArtifact } from "../lib/queries/inventory";
import { useWallet } from "../lib/queries/wallet";
import { rarityKey, rarityLabel, rarityStyle } from "../lib/rarity";

const OUTCOME_LABELS: Record<string, string> = {
  success: "Заточка вдалася",
  failure: "Заточка не вдалася — рівень не змінився",
  broken: "Зброя зламалася — полагодьте її за самоцвіти",
};

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
  repairGems: number;
  busy: boolean;
  selected: boolean;
  onSelect: () => void;
}

function EquipmentRow({ equipment, wearer, maxEnhancement, repairGems, busy, selected, onSelect }: RowProps) {
  const catalog = useCatalog();
  const atCap = equipment.enhancementLevel >= maxEnhancement;

  return (
    <button
      type="button"
      onClick={onSelect}
      disabled={busy}
      className={`flex w-full items-center gap-3 rounded-xl border p-2 text-left transition ${
        selected ? "border-emerald-500 bg-emerald-50" : equipment.isBroken ? "border-red-200 bg-white" : "border-slate-200 bg-white hover:border-slate-300"
      }`}
    >
      <ItemIcon itemKey={equipment.itemKey} type="equipment" rarity={equipment.rarity} size={40} className={equipment.isBroken ? "grayscale" : ""} />
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium text-slate-800">
          {catalog.itemName(equipment.itemKey)}
          {equipment.enhancementLevel > 0 && <span className="ml-1 text-amber-600">+{equipment.enhancementLevel}</span>}
        </div>
        <div className="flex flex-wrap gap-1 text-xs">
          <span className={`rounded px-1.5 ${rarityStyle(equipment.rarity)}`}>{rarityLabel(equipment.rarity)}</span>
          {wearer !== null && <span className="text-slate-500">{wearer}</span>}
          {equipment.isBroken && <span className="text-red-700">зламано · ремонт {repairGems} 💎</span>}
          {atCap && <span className="text-slate-500">максимум</span>}
        </div>
      </div>
    </button>
  );
}

/**
 * Кузня: заточка зброї (з ризиком зламати), ремонт за самоцвіти, прокачка
 * артефактів і купівля зброї за золото. Одягання лишається в інвентарі —
 * тут працюють із самим предметом, а не з героєм.
 */
export default function ForgePage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const catalog = useCatalog();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);
  const wallet = useWallet(playerId);

  const enhance = useEnhance(playerId);
  const repair = useRepair(playerId);
  const upgrade = useUpgradeArtifact(playerId);
  const buy = useBuyWeapon(playerId);

  const [tab, setTab] = useState<"work" | "shop">("work");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // Зламане й унікальне — вгорі: саме з ними приходять до кузні
  const equipment = useMemo(
    () =>
      [...(inventory.data?.equipment ?? [])].sort(
        (a, b) =>
          Number(b.isBroken) - Number(a.isBroken) ||
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

  const busy = enhance.isPending || repair.isPending || upgrade.isPending || buy.isPending;
  const failure = enhance.error ?? repair.error ?? upgrade.error ?? buy.error;
  const outcome = enhance.data?.outcome ?? null;
  const gems = wallet.data?.gemBalance ?? 0;

  const selected = equipment.find((item) => item.id === selectedId) ?? equipment[0] ?? null;
  const heroName = (heroId: string | null | undefined) => {
    const hero = heroes.data?.heroes.find((candidate) => candidate.id === heroId);
    return hero === undefined ? null : catalog.heroName(hero.heroKey);
  };

  // Ремонт: база плюс надбавка за рівень — та сама формула, що й на сервері
  const repairGems = (level: number) => catalog.repairGemsBase + catalog.repairGemsPerLevel * level;

  const tabs = [
    { key: "work", label: `Верстак · ${equipment.length}` },
    { key: "shop", label: "Купити зброю" },
  ] as const;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Кузня</h1>
        <p className="text-sm text-slate-500">💎 {gems.toLocaleString("uk-UA")}</p>
      </div>

      <ErrorBanner error={failure} />

      {outcome !== null && !enhance.isPending && (
        <div
          role="status"
          className={`rounded-lg px-3 py-2 text-sm ${outcome === "success" ? "bg-emerald-50 text-emerald-800" : "bg-amber-50 text-amber-900"}`}
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
            className={`rounded-lg px-3 py-1 text-sm ${tab === item.key ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"}`}
          >
            {item.label}
          </button>
        ))}
      </nav>

      {tab === "shop" && <WeaponShop busy={busy} onBuy={(itemKey) => buy.mutate(itemKey)} />}

      {tab === "work" &&
        (equipment.length === 0 ? (
          <p className="text-sm text-slate-500">Нічого точити — купіть зброю або крутіть банери.</p>
        ) : (
          <div className="grid gap-4 lg:grid-cols-[1fr_1fr]">
            <div className="max-h-[560px] space-y-2 overflow-y-auto pr-1">
              {equipment.map((item) => (
                <EquipmentRow
                  key={item.id}
                  equipment={item}
                  wearer={heroName(item.equippedByHeroId)}
                  maxEnhancement={catalog.maxEnhancement}
                  repairGems={repairGems(item.enhancementLevel)}
                  busy={busy}
                  selected={selected?.id === item.id}
                  onSelect={() => setSelectedId(item.id)}
                />
              ))}
            </div>

            {selected !== null && (
              <aside className="h-fit space-y-4 rounded-xl border border-slate-200 bg-white p-4">
                <div className="flex gap-3">
                  <ItemIcon itemKey={selected.itemKey} type="equipment" rarity={selected.rarity} size={72} className={selected.isBroken ? "grayscale" : ""} />
                  <div className="min-w-0">
                    <h2 className="text-lg font-medium text-slate-800">
                      {catalog.itemName(selected.itemKey)}
                      {selected.enhancementLevel > 0 && <span className="ml-1 text-amber-600">+{selected.enhancementLevel}</span>}
                    </h2>
                    <div className="mt-1 flex flex-wrap gap-1 text-xs">
                      <span className={`rounded px-2 py-0.5 ${rarityStyle(selected.rarity)}`}>{rarityLabel(selected.rarity)}</span>
                      <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{selected.slot === "Weapon" ? "Зброя" : "Артефакт"}</span>
                      {selected.isBroken && <span className="rounded bg-red-100 px-2 py-0.5 text-red-700">Зламано</span>}
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
                  {selected.slot === "Weapon"
                    ? " · заточка коштує золота села; понад безпечний рівень може не вдатись або зламати зброю"
                    : " · прокачка коштує золота села й завжди вдається"}
                </p>

                <div className="flex flex-wrap gap-2">
                  {selected.slot === "Weapon" && selected.isBroken && (
                    <button
                      type="button"
                      onClick={() => repair.mutate(selected.id)}
                      disabled={busy || gems < repairGems(selected.enhancementLevel)}
                      className="rounded-lg bg-violet-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50"
                    >
                      Полагодити за {repairGems(selected.enhancementLevel)} 💎
                    </button>
                  )}
                  {selected.slot === "Weapon" && !selected.isBroken && selected.enhancementLevel < catalog.maxEnhancement && (
                    <button
                      type="button"
                      onClick={() => enhance.mutate(selected.id)}
                      disabled={busy}
                      className="rounded-lg bg-amber-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-600 disabled:opacity-50"
                    >
                      Заточити до +{selected.enhancementLevel + 1}
                    </button>
                  )}
                  {selected.slot !== "Weapon" && selected.enhancementLevel < catalog.maxEnhancement && (
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
        ))}
    </div>
  );
}
