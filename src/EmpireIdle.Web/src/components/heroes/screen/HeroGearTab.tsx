import { useState } from "react";
import type { EquipmentResponse, InventoryItemResponse } from "../../../lib/apiTypes";
import { useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { GameButton, Tile } from "../../game/GameUi";
import ItemIcon from "../../inventory/ItemIcon";
import HeroPortrait from "../HeroPortrait";
import EquipmentInfoModal from "./EquipmentInfoModal";
import { WeaponDetails, WeaponSlot } from "./HeroWeapon";

interface Props {
  playerId: string;
  hero: HeroSummary;
  config: CatalogHero;
  /** Усе спорядження гравця — і вдягнене, і вільне. */
  equipment: EquipmentResponse[];
  /** Стакові предмети — для скринь зброї. */
  items: InventoryItemResponse[];
  busy: boolean;
  onEquip: (equipmentId: string) => void;
  onUnequip: (equipmentId: string) => void;
  onUnequipAll: (equipmentIds: string[]) => void;
  onEquipBest: () => void;
}

/** Обраний слот зброї — поруч із номерами артефактних слотів. */
const WEAPON = "weapon";

/** Слот артефакта героя за типом (намисто, корона…). */
interface Slot {
  index: number;
  artifactKey: string;
  label: string;
}

/**
 * Вкладка «Спорядження» (рішення 08.10.2026): чотири артефакти навколо героя й зброя під ним (GDD §6.4).
 * Порожній слот показує вільне, що туди підходить; «Швидке використання» вдягає найкраще вільне.
 */
export default function HeroGearTab({
  playerId,
  hero,
  config,
  equipment,
  items,
  busy,
  onEquip,
  onUnequip,
  onUnequipAll,
  onEquipBest,
}: Props) {
  const catalog = useCatalog();
  const [picked, setPicked] = useState<string | null>(null);
  // Вдягнений предмет відкривається окремим вікном з прокачкою (референс 13.10.2026)
  const [infoSlot, setInfoSlot] = useState<number | null>(null);

  const atHome = hero.stationedGarrisonId != null;
  const worn = equipment.filter((piece) => piece.equippedByHeroId === hero.id);

  const slots: Slot[] = catalog.artifactSlots.map((slot, index) => ({ index, artifactKey: slot.key, label: slot.displayName }));

  const slotId = (slot: Slot) => String(slot.index);
  const wornIn = (slot: Slot) => worn.find((piece) => piece.slotIndex === slot.index) ?? null;

  // Те саме правило, що EquipmentFit на сервері: вільне, ціле, не на ринку й придатне для слота
  const candidatesFor = (slot: Slot) =>
    equipment
      .filter((piece) => piece.equippedByHeroId == null && !piece.isOnMarket)
      .filter((piece) => catalog.item(piece.itemKey)?.artifactSlot === slot.artifactKey);

  const pickedSlot = slots.find((slot) => slotId(slot) === picked) ?? null;

  const slotTile = (slot: Slot) => {
    const piece = wornIn(slot);

    return piece === null ? (
      <button
        type="button"
        onClick={() => setPicked(slotId(slot))}
        className={`flex aspect-square w-full flex-col items-center justify-center rounded-xl border-2 border-dashed border-sky-300/50 bg-[#16305e]/60 text-xs text-sky-200 hover:bg-[#16305e] ${
          picked === slotId(slot) ? "outline outline-3 outline-offset-2 outline-yellow-300" : ""
        }`}
      >
        <span className="text-2xl">+</span>
        {slot.label}
        {candidatesFor(slot).length > 0 && <span className="mt-0.5 h-2 w-2 rounded-full bg-emerald-400" />}
      </button>
    ) : (
      <Tile
        rarity={piece.rarity}
        selected={infoSlot === slot.index}
        onClick={() => {
          setPicked(null);
          setInfoSlot(slot.index);
        }}
        title={catalog.itemName(piece.itemKey)}
        top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
        bottomRight={piece.mastery > 0 ? `+${piece.mastery}` : undefined}
      >
        <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={56} bare />
      </Tile>
    );
  };

  const [left1, left2, right1, right2] = slots;

  return (
    <div className="space-y-4">
      <div className="mx-auto grid max-w-md grid-cols-[5rem_1fr_5rem] items-center gap-3">
        <div className="space-y-6">
          {left1 !== undefined && slotTile(left1)}
          {left2 !== undefined && slotTile(left2)}
        </div>
        <div className="flex flex-col items-center gap-3">
          <HeroPortrait heroKey={hero.heroKey} heroClass={config.class} rank={config.rank} tier={hero.tier} size={160} />
          <WeaponSlot hero={hero} config={config} selected={picked === WEAPON} onSelect={() => setPicked(WEAPON)} />
        </div>
        <div className="space-y-6">
          {right1 !== undefined && slotTile(right1)}
          {right2 !== undefined && slotTile(right2)}
        </div>
      </div>

      {!atHome && <p className="text-center text-sm text-amber-200">Герой у поході — переодягнути його можна вдома.</p>}

      <div className="flex justify-center gap-3">
        <GameButton onClick={() => onUnequipAll(worn.map((piece) => piece.id))} disabled={busy || !atHome || worn.length === 0}>
          Зняти все
        </GameButton>
        <GameButton onClick={onEquipBest} disabled={busy || !atHome} variant="secondary" title="Найкраще вільне спорядження в кожен слот">
          Швидке використання
        </GameButton>
      </div>

      {picked === WEAPON && <WeaponDetails playerId={playerId} hero={hero} config={config} items={items} />}

      {infoSlot !== null && (
        <EquipmentInfoModal
          playerId={playerId}
          worn={[...worn].sort((a, b) => (a.slotIndex ?? 0) - (b.slotIndex ?? 0))}
          slotIndex={infoSlot}
          onSlot={setInfoSlot}
          candidatesFor={(piece) => {
            const slot = slots.find((candidate) => candidate.index === piece.slotIndex);
            return slot === undefined ? [] : candidatesFor(slot);
          }}
          locked={!atHome}
          busy={busy}
          onEquip={onEquip}
          onUnequip={onUnequip}
          onClose={() => setInfoSlot(null)}
        />
      )}

      {pickedSlot !== null && wornIn(pickedSlot) === null && (
        <SlotDetails
          slot={pickedSlot}
          candidates={candidatesFor(pickedSlot)}
          disabled={busy || !atHome}
          onEquip={onEquip}
        />
      )}
    </div>
  );
}

/** Порожній слот: вільне, що туди підходить. Вдягнене відкривається вікном EquipmentInfoModal. */
function SlotDetails({
  slot,
  candidates,
  disabled,
  onEquip,
}: {
  slot: Slot;
  candidates: EquipmentResponse[];
  disabled: boolean;
  onEquip: (equipmentId: string) => void;
}) {
  const catalog = useCatalog();

  return (
    <div className="space-y-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
      <p className="text-sm font-semibold">
        {slot.label}: {candidates.length === 0 ? "вільного спорядження немає" : "вільне, що підходить"}
      </p>
      {candidates.length > 0 && (
        <div className="grid grid-cols-5 gap-2 sm:grid-cols-8">
          {candidates.map((piece) => (
            <Tile
              key={piece.id}
              rarity={piece.rarity}
              onClick={() => onEquip(piece.id)}
              dimmed={disabled}
              title={`Вдягнути: ${catalog.itemName(piece.itemKey)}`}
              top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
              bottomRight={piece.mastery > 0 ? `+${piece.mastery}` : undefined}
            >
              <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={44} bare />
            </Tile>
          ))}
        </div>
      )}
    </div>
  );
}
