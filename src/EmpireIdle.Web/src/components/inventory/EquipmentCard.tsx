import { useState } from "react";
import { Link } from "react-router-dom";
import type { EquipmentResponse } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import type { HeroSummary } from "../../lib/queries/heroes";
import { rarityLabel, rarityStyle } from "../../lib/rarity";
import { statLabel } from "../../lib/statNames";
import ItemIcon from "./ItemIcon";

interface Props {
  equipment: EquipmentResponse;
  heroes: HeroSummary[];
  artifactSlots: number;
  busy: boolean;
  onEquip: (heroId: string, slotIndex: number) => void;
  onUnequip: () => void;
}

/**
 * Екземпляр спорядження в інвентарі: одягнути на героя вдома (для артефакта —
 * ще й слот) або зняти. Заточка, ремонт і прокачка — в кузні, це інший екран.
 */
export default function EquipmentCard({ equipment, heroes, artifactSlots, busy, onEquip, onUnequip }: Props) {
  const catalog = useCatalog();
  const [picking, setPicking] = useState(false);
  const [heroId, setHeroId] = useState("");
  const [slotIndex, setSlotIndex] = useState(0);

  const isWeapon = equipment.slot === "Weapon";
  const set = catalog.setOfItem(equipment.itemKey);
  const wearer = equipment.equippedByHeroId === null ? null : heroes.find((hero) => hero.id === equipment.equippedByHeroId);
  // Переодягаються лише вдома: герой у поході не кандидат
  const candidates = heroes.filter((hero) => hero.stationedGarrisonId != null);
  const chosenHero = heroId !== "" ? heroId : (candidates[0]?.id ?? "");

  const button = "rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50";

  return (
    <div className={`rounded-xl border bg-white p-3 ${equipment.isBroken ? "border-red-300" : "border-slate-200"}`}>
      <div className="flex gap-3">
        <ItemIcon itemKey={equipment.itemKey} type="equipment" rarity={equipment.rarity} size={48} className={equipment.isBroken ? "grayscale" : ""} />

        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-2">
            <span className="truncate font-medium text-slate-800">
              {catalog.itemName(equipment.itemKey)}
              {equipment.enhancementLevel > 0 && <span className="ml-1 text-amber-600">+{equipment.enhancementLevel}</span>}
            </span>
            <span className="text-xs text-slate-500">{isWeapon ? "Зброя" : `Артефакт`}</span>
          </div>

          <div className="mt-1 flex flex-wrap items-center gap-1 text-xs">
            <span className={`rounded px-2 py-0.5 ${rarityStyle(equipment.rarity)}`}>{rarityLabel(equipment.rarity)}</span>
            {equipment.isBroken && <span className="rounded bg-red-100 px-2 py-0.5 text-red-700">Зламано</span>}
            {set !== null && (
              <Link
                to={`/inventory/sets?set=${set.key}`}
                className="rounded bg-slate-100 px-2 py-0.5 text-slate-700 hover:bg-slate-200"
              >
                {set.displayName} · рів. {set.tier}
              </Link>
            )}
            {wearer !== null && wearer !== undefined && (
              <span className="rounded bg-emerald-100 px-2 py-0.5 text-emerald-800">
                {catalog.heroName(wearer.heroKey)}
                {!isWeapon && ` · слот ${equipment.slotIndex + 1}`}
              </span>
            )}
          </div>
        </div>
      </div>

      <dl className="mt-2 grid grid-cols-3 gap-1 text-sm">
        {Object.entries(equipment.stats).map(([stat, value]) => (
          <div key={stat} className="rounded bg-slate-50 px-2 py-1">
            <dt className="text-xs text-slate-500">{statLabel(stat)}</dt>
            <dd className="font-medium text-slate-800">{Math.round(value)}</dd>
          </div>
        ))}
      </dl>

      <div className="mt-3 flex flex-wrap items-center gap-2">
        {equipment.equippedByHeroId !== null ? (
          <button type="button" onClick={onUnequip} disabled={busy} className={button}>
            Зняти
          </button>
        ) : (
          !equipment.isBroken && (
            <button
              type="button"
              onClick={() => setPicking((previous) => !previous)}
              disabled={busy || candidates.length === 0}
              className={button}
            >
              Одягнути
            </button>
          )
        )}

        {equipment.isBroken && <span className="text-xs text-slate-500">Полагодити можна в кузні</span>}
      </div>

      {picking && (
        <div className="mt-3 flex flex-wrap items-center gap-2 rounded-lg bg-slate-50 p-2">
          <select
            value={chosenHero}
            onChange={(event) => setHeroId(event.target.value)}
            className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm"
          >
            {candidates.map((hero) => (
              <option key={hero.id} value={hero.id}>
                {catalog.heroName(hero.heroKey)} · {hero.level} рів.
              </option>
            ))}
          </select>

          {!isWeapon && (
            <select
              value={slotIndex}
              onChange={(event) => setSlotIndex(Number(event.target.value))}
              className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm"
            >
              {Array.from({ length: artifactSlots }, (_, index) => (
                <option key={index} value={index}>
                  Слот {index + 1}
                </option>
              ))}
            </select>
          )}

          <button
            type="button"
            onClick={() => {
              onEquip(chosenHero, isWeapon ? 0 : slotIndex);
              setPicking(false);
            }}
            disabled={busy || chosenHero === ""}
            className="ml-auto rounded-lg bg-emerald-600 px-3 py-1 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
          >
            Підтвердити
          </button>
        </div>
      )}
    </div>
  );
}
