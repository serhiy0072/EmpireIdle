import { useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GamePanel, GameScreen, GameTabs, Tile, TileGrid, type GameTab } from "../components/game/GameUi";
import HeroPortrait from "../components/heroes/HeroPortrait";
import EquipmentInfoModal from "../components/heroes/screen/EquipmentInfoModal";
import ActiveEffects from "../components/inventory/ActiveEffects";
import ItemCard from "../components/inventory/ItemCard";
import ArtifactHover from "../components/inventory/ArtifactHover";
import ItemIcon from "../components/inventory/ItemIcon";
import LootboxCard from "../components/lootboxes/LootboxCard";
import { LootboxDrops, WeaponChestContents } from "../components/lootboxes/LootboxPanels";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { EquipmentResponse, InventoryItemResponse, LootboxDrop } from "../lib/apiTypes";
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
  energy: "bonus",
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
  const [drops, setDrops] = useState<LootboxDrop[] | null>(null);

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

  // Ящик ресурсів лишається в «Ресурсах», навіть коли він скриня з випадковою сумою, як ящик золота (рішення 22.10.2026)
  const tabOf = (item: { itemKey: string; type: string }) =>
    item.itemKey.startsWith("crate_") ? "resources" : (TAB_OF_TYPE[item.type] ?? "other");
  const shown = items.filter((item) => tabOf(item) === tab);

  const tabs: GameTab<Tab>[] = [
    { key: "resources", label: "Ресурси" },
    { key: "speedups", label: "Прискорення" },
    { key: "bonus", label: "Бонус" },
    { key: "equipment", label: "Спорядж." },
    { key: "other", label: "Інше" },
  ];

  const pickedItem = tab === "equipment" ? null : (shown.find((item) => item.itemKey === selected) ?? null);
  const pickedEquipment = tab === "equipment" ? (equipment.find((piece) => piece.id === selected) ?? null) : null;

  // Вдягнене тим самим героєм — для лічильника набору
  const wornKeysOf = (piece: EquipmentResponse) =>
    piece.equippedByHeroId == null
      ? []
      : equipment.filter((other) => other.equippedByHeroId === piece.equippedByHeroId).map((other) => other.itemKey);

  // Переодягаються лише вдома; набір — лише для свого класу (рішення 15.10.2026)
  const wearersFor = (piece: EquipmentResponse) => {
    const setClass = catalog.setOfItem(piece.itemKey)?.class;
    return roster.filter((hero) => hero.stationedGarrisonId != null && (setClass == null || catalog.hero(hero.heroKey)?.class === setClass));
  };

  // Стрілки вікна гортають рюкзак у порядку сітки
  const stepEquipment = (delta: number) => {
    const index = equipment.findIndex((piece) => piece.id === selected);
    const next = equipment[(index + delta + equipment.length) % equipment.length];
    if (next !== undefined) select({ item: next.id });
  };

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
                    <ArtifactHover key={piece.id} artifact={piece} wornKeys={wornKeysOf(piece)}>
                    <Tile
                      rarity={piece.rarity}
                      selected={piece.id === selected}
                      onClick={() => select({ item: piece.id })}
                      top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
                      bottomRight={piece.mastery > 0 ? `+${piece.mastery}` : undefined}
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
                    </ArtifactHover>
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

      {drops !== null && <LootboxDrops drops={drops} onClose={() => setDrops(null)} />}

      {pickedItem !== null && pickedItem.type === "lootbox" && (
        <LootboxCard playerId={playerId} item={pickedItem} onOpened={setDrops} />
      )}

      {/* Дії — у світлій картці під сіткою: ті самі, що були, без дублювання логіки */}
      {pickedItem !== null && pickedItem.type !== "lootbox" && (
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
          {pickedItem.type === "weaponchest" && (
            <div className="px-1">
              <WeaponChestContents itemKey={pickedItem.itemKey} />
            </div>
          )}
          {pickedItem.type === "speedup" && (
            <p className="px-1 text-xs text-slate-500">
              Прискорення вживається з таймера: натисніть «Прискорити» біля будівництва, тренування чи маршу.
            </p>
          )}
        </div>
      )}

      {pickedEquipment !== null && (
        <EquipmentInfoModal
          key={pickedEquipment.id}
          playerId={playerId}
          piece={pickedEquipment}
          wornKeys={wornKeysOf(pickedEquipment)}
          onStep={equipment.length > 1 ? stepEquipment : null}
          actions={(piece, choose) =>
            piece.isOnMarket ? (
              <Link to="/market?tab=mine" className="text-xs text-amber-700 hover:underline">
                До своїх лотів
              </Link>
            ) : piece.equippedByHeroId != null ? (
              <GameButton onClick={() => unequip.mutate(piece.id)} disabled={busy} variant="accent">
                Зняти
              </GameButton>
            ) : (
              <GameButton onClick={choose} disabled={busy || wearersFor(piece).length === 0}>
                Одягнути
              </GameButton>
            )
          }
          choice={{
            title: "Кому вдягнути",
            render: (piece, done) => (
              <div className="space-y-2">
                {catalog.artifactSlotName(piece.itemKey) !== null && (
                  <p className="text-xs text-slate-600">
                    Замінить те, що в слоті «{catalog.artifactSlotName(piece.itemKey)?.toLowerCase()}».
                  </p>
                )}
                <div className="grid grid-cols-4 gap-2">
                  {wearersFor(piece).map((hero) => {
                    const config = catalog.hero(hero.heroKey);

                    return (
                      <button
                        key={hero.id}
                        type="button"
                        disabled={busy}
                        onClick={() => {
                          equip.mutate({ heroId: hero.id, equipmentId: piece.id });
                          done();
                        }}
                        className="flex flex-col items-center gap-1 rounded-lg p-1 text-xs hover:bg-sky-100 disabled:opacity-50"
                      >
                        <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={56} />
                        <span className="w-full truncate text-center">{catalog.heroName(hero.heroKey)}</span>
                        <span className="text-slate-500">{hero.level} рів.</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            ),
          }}
          onClose={() => select({ item: null })}
        />
      )}
    </div>
  );
}
