import { useState } from "react";
import type { EquipmentResponse } from "../../../lib/apiTypes";
import { useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { statLabel } from "../../../lib/statNames";
import { GameButton, Tile } from "../../game/GameUi";
import ItemIcon from "../../inventory/ItemIcon";
import HeroPortrait from "../HeroPortrait";

interface Props {
  hero: HeroSummary;
  config: CatalogHero;
  /** Усе спорядження гравця — і вдягнене, і вільне. */
  equipment: EquipmentResponse[];
  busy: boolean;
  onEquip: (equipmentId: string) => void;
  onUnequip: (equipmentId: string) => void;
  onUnequipAll: (equipmentIds: string[]) => void;
  onEquipBest: () => void;
}

/** Слот артефакта героя за типом (намисто, корона…). */
interface Slot {
  index: number;
  artifactKey: string;
  label: string;
}

/**
 * Вкладка «Спорядження» (рішення 08.10.2026): чотири артефакти навколо героя.
 * Порожній слот показує вільне, що туди підходить; «Швидке використання» вдягає найкраще вільне.
 */
export default function HeroGearTab({ hero, config, equipment, busy, onEquip, onUnequip, onUnequipAll, onEquipBest }: Props) {
  const catalog = useCatalog();
  const [picked, setPicked] = useState<string | null>(null);

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
        selected={picked === slotId(slot)}
        onClick={() => setPicked(slotId(slot))}
        title={catalog.itemName(piece.itemKey)}
        top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
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
        <div className="flex justify-center">
          <HeroPortrait heroKey={hero.heroKey} heroClass={config.class} rank={config.rank} tier={hero.tier} size={160} />
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

      {pickedSlot !== null && (
        <SlotDetails
          slot={pickedSlot}
          worn={wornIn(pickedSlot)}
          candidates={candidatesFor(pickedSlot)}
          disabled={busy || !atHome}
          onEquip={onEquip}
          onUnequip={onUnequip}
        />
      )}
    </div>
  );
}

function SlotDetails({
  slot,
  worn,
  candidates,
  disabled,
  onEquip,
  onUnequip,
}: {
  slot: Slot;
  worn: EquipmentResponse | null;
  candidates: EquipmentResponse[];
  disabled: boolean;
  onEquip: (equipmentId: string) => void;
  onUnequip: (equipmentId: string) => void;
}) {
  const catalog = useCatalog();

  return (
    <div className="space-y-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
      {worn !== null && (
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="font-semibold">
              {catalog.itemName(worn.itemKey)}
              {worn.level > 0 && <span className="text-amber-200"> · рів. {worn.level}</span>}
              {worn.mastery > 0 && <span className="text-sky-200"> · майст. {worn.mastery}</span>}
            </p>
            <p className="text-sm text-sky-100/90">
              {Object.entries(worn.stats)
                .map(([stat, value]) => `${statLabel(stat)} +${Math.round(value)}`)
                .join(" · ")}
            </p>
          </div>
          <GameButton onClick={() => onUnequip(worn.id)} disabled={disabled} variant="secondary">
            Зняти
          </GameButton>
        </div>
      )}

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
            >
              <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={44} bare />
            </Tile>
          ))}
        </div>
      )}
    </div>
  );
}
