import { useMemo } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { GamePanel, GameScreen, GameTabs, Tile, TileGrid, type GameTab } from "../components/game/GameUi";
import HeroPortrait from "../components/heroes/HeroPortrait";
import ActiveEffects from "../components/inventory/ActiveEffects";
import EquipmentCard from "../components/inventory/EquipmentCard";
import ItemCard from "../components/inventory/ItemCard";
import ItemIcon from "../components/inventory/ItemIcon";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse, InventoryItemResponse } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { useHeroes } from "../lib/queries/heroes";
import { useEquip, useInventory, useUnequip, useUseItem } from "../lib/queries/inventory";
import { rarityKey } from "../lib/rarity";
import { speedUpMinutesLabel } from "../lib/speedUp";

type Tab = "resources" | "speedups" | "bonus" | "equipment" | "other";

/** Вкладка рюкзака за типом предмета; чого немає в списку — те «Інше». */
const TAB_OF_TYPE: Record<string, Tab> = {
  resources: "resources",
  heroxp: "resources",
  feed: "resources",
  speedup: "speedups",
  boost: "bonus",
  scoutveil: "bonus",
};

const RARITY_ORDER: Record<string, number> = { Unique: 0, Rare: 1, Common: 2 };

const byRarity = (a: { rarity: string | number }, b: { rarity: string | number }) =>
  (RARITY_ORDER[rarityKey(a.rarity)] ?? 9) - (RARITY_ORDER[rarityKey(b.rarity)] ?? 9);

/** Одягнуте — вгорі, далі рідкість і заточка: те, що гравець шукає найчастіше. */
function sortEquipment(items: EquipmentResponse[]): EquipmentResponse[] {
  return [...items].sort(
    (a, b) =>
      Number(b.equippedByHeroId != null) - Number(a.equippedByHeroId != null) ||
      byRarity(a, b) ||
      b.level - a.level ||
      b.mastery - a.mastery ||
      a.itemKey.localeCompare(b.itemKey),
  );
}

/** Прискорення — від коротких до довгих, як на полиці; решта — за рідкістю й назвою. */
function sortItems(items: InventoryItemResponse[], minutes: (key: string) => number): InventoryItemResponse[] {
  return [...items].sort(
    (a, b) => minutes(a.itemKey) - minutes(b.itemKey) || byRarity(a, b) || a.displayName.localeCompare(b.displayName, "uk"),
  );
}

export default function InventoryPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();
  const catalog = useCatalog();
  const navigate = useNavigate();

  const inventory = useInventory(playerId);
  const heroes = useHeroes(playerId);
  const consume = useUseItem(playerId);
  const equip = useEquip(playerId);
  const unequip = useUnequip(playerId);

  const [params, setParams] = useSearchParams();
  const tab = (params.get("tab") as Tab | null) ?? "resources";
  const selected = params.get("item");
  const select = (next: { tab?: Tab; item?: string | null }) =>
    setParams(
      (current) => {
        const result = new URLSearchParams(current);
        if (next.tab !== undefined) result.set("tab", next.tab);
        if (next.item === null || next.tab !== undefined) result.delete("item");
        if (next.item != null) result.set("item", next.item);
        return result;
      },
      { replace: true },
    );

  const minutes = (key: string) => catalog.item(key)?.speedUpMinutes ?? 0;

  const items = useMemo(
    () => sortItems(inventory.data?.items ?? [], (key) => catalog.item(key)?.speedUpMinutes ?? 0),
    [inventory.data?.items, catalog],
  );
  const equipment = useMemo(() => sortEquipment(inventory.data?.equipment ?? []), [inventory.data?.equipment]);

  if (inventory.isPending) {
    return <p className="text-slate-500">Завантаження рюкзака…</p>;
  }

  if (inventory.isError) {
    return <ErrorBanner error={inventory.error} />;
  }

  const busy = consume.isPending || equip.isPending || unequip.isPending;
  const failure = consume.error ?? equip.error ?? unequip.error;
  const roster = heroes.data?.heroes ?? [];

  const inTab = (type: string) => (TAB_OF_TYPE[type] ?? "other") === tab;
  const shown = items.filter((item) => inTab(item.type));

  const tabs: GameTab<Tab>[] = [
    { key: "resources", label: "Ресурси" },
    { key: "speedups", label: "Прискорення" },
    { key: "bonus", label: "Бонус" },
    { key: "equipment", label: "Спорядж." },
    { key: "other", label: "Інше" },
  ];

  const pickedItem = tab === "equipment" ? null : (shown.find((item) => item.itemKey === selected) ?? null);
  const pickedEquipment = tab === "equipment" ? (equipment.find((piece) => piece.id === selected) ?? null) : null;

  return (
    <div className="space-y-3">
      <GameScreen
        title="Рюкзак"
        actions={
          <div className="flex items-center gap-3 text-sm">
            <ActiveEffects effects={inventory.data.activeEffects} now={now} />
            <Link to="/inventory/sets" className="text-sky-200 hover:underline">
              набори
            </Link>
          </div>
        }
      >
        <GameTabs tabs={tabs} value={tab} onChange={(next) => select({ tab: next })} />
        <GamePanel className="min-h-[40vh]">
          {tab === "equipment" ? (
            equipment.length === 0 ? (
              <p className="py-8 text-center text-sm text-sky-100/80">Спорядження ще немає — купіть зброю в кузні або крутіть банери.</p>
            ) : (
              <TileGrid>
                {equipment.map((piece) => {
                  const wearer = roster.find((hero) => hero.id === piece.equippedByHeroId);
                  const wearerConfig = wearer === undefined ? null : catalog.hero(wearer.heroKey);

                  return (
                    <Tile
                      key={piece.id}
                      rarity={piece.rarity}
                      selected={piece.id === selected}
                      onClick={() => select({ item: piece.id })}
                      title={catalog.itemName(piece.itemKey)}
                      top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
                      bottomLeft={
                        wearer === undefined ? undefined : (
                          <HeroPortrait
                            heroKey={wearer.heroKey}
                            heroClass={wearerConfig?.class}
                            rank={wearerConfig?.rank}
                            tier={wearer.tier}
                            size={22}
                            className="rounded ring-1 ring-white"
                          />
                        )
                      }
                    >
                      <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={56} bare />
                    </Tile>
                  );
                })}
              </TileGrid>
            )
          ) : shown.length === 0 ? (
            <p className="py-8 text-center text-sm text-sky-100/80">
              {tab === "speedups"
                ? "Прискорень ще немає — їх дають квести, щоденні активності й скрині."
                : "Порожньо. Предмети дають квести й банери."}
            </p>
          ) : (
            <TileGrid>
              {shown.map((item) => (
                <Tile
                  key={item.itemKey}
                  rarity={item.rarity}
                  count={item.count}
                  selected={item.itemKey === selected}
                  onClick={() => select({ item: item.itemKey })}
                  title={item.displayName}
                  top={item.type === "speedup" ? speedUpMinutesLabel(minutes(item.itemKey)) : undefined}
                >
                  <ItemIcon itemKey={item.itemKey} type={item.type} rarity={item.rarity} size={56} bare />
                </Tile>
              ))}
            </TileGrid>
          )}
        </GamePanel>
      </GameScreen>

      <ErrorBanner error={failure} />

      {/* Дії — у світлій картці під сіткою: ті самі, що були, без дублювання логіки */}
      {pickedItem !== null && (
        <div className="space-y-1">
          <ItemCard
            playerId={playerId}
            item={pickedItem}
            busy={busy}
            onUse={(request) =>
              consume.mutate(request, {
                // Телепорт без обраної клітини: місце обрала гра — ведемо на мапу, де видно, куди переїхало село
                onSuccess: () => {
                  if (pickedItem.type === "teleport") void navigate("/map");
                },
              })
            }
          />
          {pickedItem.type === "speedup" && (
            <p className="px-1 text-xs text-slate-500">
              Прискорення вживається з таймера: натисніть «Прискорити» біля будівництва, тренування чи маршу.
            </p>
          )}
        </div>
      )}

      {pickedEquipment !== null && (
        <EquipmentCard
          equipment={pickedEquipment}
          heroes={roster}
          busy={busy}
          onEquip={(heroId) => equip.mutate({ heroId, equipmentId: pickedEquipment.id })}
          onUnequip={() => unequip.mutate(pickedEquipment.id)}
        />
      )}
    </div>
  );
}
