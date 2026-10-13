import { useEffect, useState } from "react";
import type { EquipmentResponse } from "../../../lib/apiTypes";
import { useCatalog } from "../../../lib/queries/catalog";
import { rarityLabel, rarityStyle } from "../../../lib/rarity";
import { statLabel, statValue } from "../../../lib/statNames";
import { EquipmentFeed, EquipmentMastery } from "../../forge/EquipmentUpgrade";
import { GameButton, Tile } from "../../game/GameUi";
import ItemIcon from "../../inventory/ItemIcon";

type View = "info" | "replace" | "feed" | "mastery";

const TITLES: Record<View, string> = {
  info: "Інформація про спорядження",
  replace: "Замінити спорядження",
  feed: "Посилення",
  mastery: "Майстерність коваля",
};

interface Props {
  playerId: string;
  /** Вдягнене героєм у порядку слотів — стрілки гортають між ним. */
  worn: EquipmentResponse[];
  /** Слот, який показує вікно: після заміни в ньому з'являється новий предмет, і вікно лишається відкритим. */
  slotIndex: number;
  onSlot: (slotIndex: number) => void;
  /** Вільне, що підходить у слот показаного предмета. */
  candidatesFor: (piece: EquipmentResponse) => EquipmentResponse[];
  /** Герой у поході: переодягати не можна, качати — можна. */
  locked: boolean;
  busy: boolean;
  onEquip: (equipmentId: string) => void;
  onUnequip: (equipmentId: string) => void;
  onClose: () => void;
}

/**
 * Вікно спорядження героя (референс 13.10.2026): предмет, його показники й майстерність, зняти чи замінити,
 * а «Посилити» й «Майстерність коваля» відкривають прокачку тут же — без переходу в Кузню.
 */
export default function EquipmentInfoModal({
  playerId,
  worn,
  slotIndex,
  onSlot,
  candidatesFor,
  locked,
  busy,
  onEquip,
  onUnequip,
  onClose,
}: Props) {
  const catalog = useCatalog();
  const [view, setView] = useState<View>("info");

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  const index = worn.findIndex((piece) => piece.slotIndex === slotIndex);
  const piece = worn[index];

  // Слот спорожнів — вікну більше нічого показувати
  if (piece === undefined) return null;

  const step = (delta: number) => {
    const next = worn[(index + delta + worn.length) % worn.length];
    if (next?.slotIndex != null) {
      onSlot(next.slotIndex);
      setView("info");
    }
  };

  const candidates = candidatesFor(piece);
  const arrow = "absolute top-1/2 -translate-y-1/2 rounded-full bg-sky-100/90 px-3 py-2 text-xl font-bold text-sky-700 shadow hover:bg-white";

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={TITLES[view]}
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
    >
      <div onClick={(event) => event.stopPropagation()} className="relative w-full max-w-md">
        {worn.length > 1 && (
          <>
            <button type="button" aria-label="Попереднє" onClick={() => step(-1)} className={`${arrow} -left-14`}>
              ‹
            </button>
            <button type="button" aria-label="Наступне" onClick={() => step(1)} className={`${arrow} -right-14`}>
              ›
            </button>
          </>
        )}

        <div className="flex max-h-[85vh] flex-col overflow-hidden rounded-2xl bg-sky-50 text-slate-800 shadow-2xl ring-2 ring-sky-300">
          <div className="flex items-center gap-2 bg-gradient-to-b from-sky-500 to-sky-600 px-4 py-2 text-white">
            {view !== "info" && (
              <button type="button" aria-label="Назад" onClick={() => setView("info")} className="text-lg font-bold hover:text-sky-100">
                ←
              </button>
            )}
            <h2 className="flex-1 text-center text-lg font-semibold">{TITLES[view]}</h2>
            <button type="button" aria-label="Закрити" onClick={onClose} className="text-xl font-bold hover:text-sky-100">
              ✕
            </button>
          </div>

          <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
            <div className="flex items-start gap-3 border-b border-sky-200 pb-3">
              <div className="relative">
                <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={64} />
                {piece.mastery > 0 && (
                  <span className="absolute -right-1 -top-1 rounded bg-slate-800/85 px-1 text-xs font-semibold text-white">
                    +{piece.mastery}
                  </span>
                )}
                <span className="absolute inset-x-0 -bottom-1 text-center text-xs font-semibold text-amber-700">Рів. {piece.level}</span>
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold">{catalog.itemName(piece.itemKey)}</p>
                <span className={`inline-block rounded px-2 py-0.5 text-xs ${rarityStyle(piece.rarity)}`}>{rarityLabel(piece.rarity)}</span>
                <p className="mt-1 text-sm text-slate-600">⚔ {Math.round(piece.power).toLocaleString("uk-UA")}</p>
                {piece.isBroken && <p className="text-xs text-rose-700">Зламано: дає половину статів</p>}
              </div>
              {view === "info" && (
                <div className="flex shrink-0 flex-col gap-1">
                  <GameButton
                    onClick={() => {
                      onUnequip(piece.id);
                      onClose();
                    }}
                    disabled={busy || locked}
                    variant="accent"
                  >
                    Зняти
                  </GameButton>
                  <GameButton onClick={() => setView("replace")} disabled={busy || locked || candidates.length === 0}>
                    Замінити
                  </GameButton>
                </div>
              )}
            </div>

            {view === "info" && (
              <>
                <section className="space-y-1">
                  <h3 className="text-sm font-semibold">Показники спорядження</h3>
                  <dl className="divide-y divide-sky-100 rounded-lg bg-white/80 text-sm">
                    {Object.entries(piece.stats).map(([stat, value]) => (
                      <div key={stat} className="flex justify-between px-3 py-1.5">
                        <dt className="text-slate-600">{statLabel(stat)}</dt>
                        <dd className="font-semibold tabular-nums">{statValue(stat, value)}</dd>
                      </div>
                    ))}
                  </dl>
                </section>
                <section className="space-y-1">
                  <h3 className="text-sm font-semibold">Майстерність коваля</h3>
                  <p className="rounded-lg bg-white/80 px-3 py-1.5 text-sm text-slate-600">
                    {piece.mastery} з {catalog.maxMastery}
                    {piece.experienceToNext != null && ` · до рівня ${piece.level + 1} — ${piece.experienceToNext} досвіду`}
                  </p>
                </section>
                {locked && <p className="text-xs text-amber-700">Герой у поході: зняти чи замінити можна вдома, качати — вже зараз.</p>}
              </>
            )}

            {view === "replace" && (
              <div className="grid grid-cols-5 gap-2">
                {candidates.map((candidate) => (
                  <Tile
                    key={candidate.id}
                    rarity={candidate.rarity}
                    onClick={() => {
                      onEquip(candidate.id);
                      setView("info");
                    }}
                    dimmed={busy}
                    title={`Вдягнути: ${catalog.itemName(candidate.itemKey)} · ⚔ ${Math.round(candidate.power)}`}
                    top={candidate.level > 0 ? `Рів. ${candidate.level}` : undefined}
                  >
                    <ItemIcon itemKey={candidate.itemKey} type="equipment" rarity={candidate.rarity} size={44} bare />
                  </Tile>
                ))}
              </div>
            )}

            {view === "feed" && (
              <section className="space-y-2">
                <EquipmentFeed key={piece.id} playerId={playerId} selected={piece} />
              </section>
            )}

            {view === "mastery" && (
              <section className="space-y-2">
                <EquipmentMastery playerId={playerId} selected={piece} />
              </section>
            )}
          </div>

          {view === "info" && (
            <div className="grid grid-cols-2 gap-3 border-t border-sky-200 p-4">
              <GameButton onClick={() => setView("mastery")}>Майстерність коваля</GameButton>
              <GameButton onClick={() => setView("feed")} variant="accent">
                Посилити
              </GameButton>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
