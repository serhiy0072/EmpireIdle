import { useState } from "react";
import type { EquipmentResponse, InventoryItemResponse } from "../../../lib/apiTypes";
import { useCatalog, type CatalogHero } from "../../../lib/queries/catalog";
import type { HeroSummary } from "../../../lib/queries/heroes";
import { GameButton, Tile } from "../../game/GameUi";
import ArtifactHover from "../../inventory/ArtifactHover";
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

  // Те саме правило, що EquipmentFit на сервері: вільне, не на ринку, придатне для слота й свого класу
  const freeFor = (slot: Slot) =>
    equipment
      .filter((piece) => piece.equippedByHeroId == null && !piece.isOnMarket)
      .filter((piece) => catalog.item(piece.itemKey)?.artifactSlot === slot.artifactKey);
  const fitsClass = (piece: EquipmentResponse) => {
    const setClass = catalog.setOfItem(piece.itemKey)?.class;
    return setClass == null || setClass === config.class;
  };
  const candidatesFor = (slot: Slot) => freeFor(slot).filter(fitsClass);
  // Набори інших класів цей герой не вдягне (рішення 15.10.2026) — лише кажемо, що вони є
  const foreignFor = (slot: Slot) => freeFor(slot).filter((piece) => !fitsClass(piece)).length;

  const pickedSlot = slots.find((slot) => slotId(slot) === picked) ?? null;

  // Вікно вдягненого гортає слоти по порядку; заміна лишає його на тому ж слоті
  const wornBySlot = [...worn].sort((a, b) => (a.slotIndex ?? 0) - (b.slotIndex ?? 0));
  const stepSlot = (delta: number) => {
    const index = wornBySlot.findIndex((piece) => piece.slotIndex === infoSlot);
    const next = wornBySlot[(index + delta + wornBySlot.length) % wornBySlot.length];
    if (next?.slotIndex != null) setInfoSlot(next.slotIndex);
  };
  const slotCandidates = (piece: EquipmentResponse) => {
    const slot = slots.find((candidate) => candidate.index === piece.slotIndex);
    return slot === undefined ? [] : candidatesFor(slot);
  };

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
      <ArtifactHover artifact={piece} wornKeys={worn.map((item) => item.itemKey)}>
        <Tile
          rarity={piece.rarity}
          selected={infoSlot === slot.index}
          onClick={() => {
            setPicked(null);
            setInfoSlot(slot.index);
          }}
          top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
          bottomRight={piece.mastery > 0 ? `+${piece.mastery}` : undefined}
        >
          <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={56} bare />
        </Tile>
      </ArtifactHover>
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
        <GameButton
          onClick={onEquipBest}
          disabled={busy || !atHome}
          variant="secondary"
          title="Найкраще вільне спорядження в кожен слот"
          tutorial="equip-best"
        >
          Швидке використання
        </GameButton>
      </div>

      {picked === WEAPON && <WeaponDetails playerId={playerId} hero={hero} config={config} items={items} />}

      {infoSlot !== null && (
        <EquipmentInfoModal
          key={wornBySlot.find((piece) => piece.slotIndex === infoSlot)?.id}
          playerId={playerId}
          piece={wornBySlot.find((piece) => piece.slotIndex === infoSlot)}
          wornKeys={worn.map((item) => item.itemKey)}
          onStep={wornBySlot.length > 1 ? stepSlot : null}
          actions={(piece, choose) => (
            <>
              <GameButton
                onClick={() => {
                  onUnequip(piece.id);
                  setInfoSlot(null);
                }}
                disabled={busy || !atHome}
                variant="accent"
              >
                Зняти
              </GameButton>
              <GameButton onClick={choose} disabled={busy || !atHome || slotCandidates(piece).length === 0}>
                Замінити
              </GameButton>
            </>
          )}
          choice={{
            title: "Замінити спорядження",
            render: (piece, done) => (
              <div className="grid grid-cols-5 gap-2">
                {slotCandidates(piece).map((candidate) => (
                  <Tile
                    key={candidate.id}
                    rarity={candidate.rarity}
                    onClick={() => {
                      onEquip(candidate.id);
                      done();
                    }}
                    dimmed={busy}
                    title={`Вдягнути: ${catalog.itemName(candidate.itemKey)} · ⚔ ${Math.round(candidate.power)}`}
                    top={candidate.level > 0 ? `Рів. ${candidate.level}` : undefined}
                    bottomRight={candidate.mastery > 0 ? `+${candidate.mastery}` : undefined}
                  >
                    <ItemIcon itemKey={candidate.itemKey} type="equipment" rarity={candidate.rarity} size={44} bare />
                  </Tile>
                ))}
              </div>
            ),
          }}
          note={!atHome && <p className="text-xs text-amber-700">Герой у поході: зняти чи замінити можна вдома, качати — вже зараз.</p>}
          onClose={() => setInfoSlot(null)}
        />
      )}

      {pickedSlot !== null && wornIn(pickedSlot) === null && (
        <SlotDetails
          slot={pickedSlot}
          candidates={candidatesFor(pickedSlot)}
          wornKeys={worn.map((item) => item.itemKey)}
          foreign={foreignFor(pickedSlot)}
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
  wornKeys,
  foreign,
  disabled,
  onEquip,
}: {
  slot: Slot;
  candidates: EquipmentResponse[];
  /** Вдягнене героєм — підказка показує, скільки предметів набору вже на ньому. */
  wornKeys: string[];
  /** Скільки вільних предметів цього слота — з наборів іншого класу. */
  foreign: number;
  disabled: boolean;
  onEquip: (equipmentId: string) => void;
}) {
  return (
    <div className="space-y-3 rounded-xl bg-[#16305e]/80 p-3 ring-1 ring-[#335c9a]">
      <p className="text-sm font-semibold">
        {slot.label}: {candidates.length === 0 ? "вільного спорядження немає" : "вільне, що підходить"}
      </p>
      {candidates.length > 0 && (
        <div className="grid grid-cols-5 gap-2 sm:grid-cols-8">
          {candidates.map((piece) => (
            <ArtifactHover key={piece.id} artifact={piece} wornKeys={wornKeys}>
              <Tile
                rarity={piece.rarity}
                onClick={() => onEquip(piece.id)}
                dimmed={disabled}
                top={piece.level > 0 ? `Рів. ${piece.level}` : undefined}
                bottomRight={piece.mastery > 0 ? `+${piece.mastery}` : undefined}
              >
                <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={44} bare />
              </Tile>
            </ArtifactHover>
          ))}
        </div>
      )}
      {foreign > 0 && (
        <p className="text-xs text-sky-200/80">Ще {foreign} — з наборів іншого класу: цей герой їх не вдягне.</p>
      )}
    </div>
  );
}
