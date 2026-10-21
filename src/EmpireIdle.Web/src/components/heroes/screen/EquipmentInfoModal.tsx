import { useState, type ReactNode } from "react";
import type { EquipmentResponse } from "../../../lib/apiTypes";
import { useCatalog } from "../../../lib/queries/catalog";
import { rarityLabel, rarityStyle } from "../../../lib/rarity";
import { statLabel, statValue } from "../../../lib/statNames";
import { EquipmentFeed, EquipmentMastery } from "../../forge/EquipmentUpgrade";
import { GameButton } from "../../game/GameUi";
import ArtifactSetInfo from "../../inventory/ArtifactSetInfo";
import ItemIcon from "../../inventory/ItemIcon";
import { useHotkeys } from "../../../hooks/useHotkeys";

type View = "info" | "choice" | "feed" | "mastery";

const TITLES: Record<Exclude<View, "choice">, string> = {
  info: "Інформація про спорядження",
  feed: "Посилення",
  mastery: "Майстерність коваля",
};

/** Вид вибору поверх головного: заміна в слоті героя чи герой, на якого вдягнути. */
export interface EquipmentChoice {
  title: string;
  /** done повертає на головний вид. */
  render: (piece: EquipmentResponse, done: () => void) => ReactNode;
}

interface Props {
  playerId: string;
  /** Показаний предмет; undefined — його вже немає (зняли чи продали), і вікно нічого не малює. */
  piece: EquipmentResponse | undefined;
  /** Вдягнене на того самого героя — для лічильника набору. */
  wornKeys: string[];
  /** Гортання сусідніх предметів стрілками; null — стрілок немає. */
  onStep: ((delta: number) => void) | null;
  /** Кнопки біля предмета на головному виді; choose відкриває вид вибору. */
  actions: (piece: EquipmentResponse, choose: () => void) => ReactNode;
  choice: EquipmentChoice;
  /** Підказка під показниками, як-от «герой у поході». */
  note?: ReactNode;
  onClose: () => void;
}

/**
 * Вікно спорядження (референс 13.10.2026): предмет, його показники й майстерність, дії з ним,
 * а «Посилити» й «Майстерність коваля» відкривають прокачку тут же: окремої Кузні немає.
 * Те саме вікно — і з героя, і з рюкзака; різняться лише дії й вид вибору. Батько ставить key={piece.id}:
 * інший предмет відкривається з головного виду.
 */
export default function EquipmentInfoModal({ playerId, piece, wornKeys, onStep, actions, choice, note, onClose }: Props) {
  const catalog = useCatalog();
  const [view, setView] = useState<View>("info");

  useHotkeys(
    {
      Escape: onClose,
      ArrowLeft: onStep === null ? undefined : () => onStep(-1),
      ArrowRight: onStep === null ? undefined : () => onStep(1),
    },
    { modal: true },
  );

  if (piece === undefined) return null;

  // Виставлене на ринок не прокачується — спершу зняти лот
  const upgradable = !piece.isOnMarket;
  const title = view === "choice" ? choice.title : TITLES[view];
  const arrow = "absolute top-1/2 -translate-y-1/2 rounded-full bg-sky-100/90 px-3 py-2 text-xl font-bold text-sky-700 shadow hover:bg-white";

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
    >
      <div onClick={(event) => event.stopPropagation()} className="relative w-full max-w-md">
        {onStep !== null && (
          <>
            <button type="button" aria-label="Попереднє" onClick={() => onStep(-1)} className={`${arrow} -left-14`}>
              ‹
            </button>
            <button type="button" aria-label="Наступне" onClick={() => onStep(1)} className={`${arrow} -right-14`}>
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
            <h2 className="flex-1 text-center text-lg font-semibold">{title}</h2>
            <button type="button" aria-label="Закрити" onClick={onClose} className="text-xl font-bold hover:text-sky-100">
              ✕
            </button>
          </div>

          <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
            <div className="flex items-start gap-3 border-b border-sky-200 pb-3">
              <div className="relative">
                <ItemIcon itemKey={piece.itemKey} type="equipment" rarity={piece.rarity} size={64} mastery={piece.mastery} />
                {/* Рівень угорі, заточка внизу праворуч — як на плитках рюкзака */}
                <span className="absolute inset-x-0 top-0.5 text-center text-xs font-bold text-white [text-shadow:0_1px_2px_#000]">
                  Рів. {piece.level}
                </span>
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold">{catalog.itemName(piece.itemKey)}</p>
                <span className={`inline-block rounded px-2 py-0.5 text-xs ${rarityStyle(piece.rarity)}`}>{rarityLabel(piece.rarity)}</span>
                <p className="mt-1 text-sm text-slate-600">⚔ {Math.round(piece.power).toLocaleString("uk-UA")}</p>
                {piece.isBroken && <p className="text-xs text-rose-700">Зламано: дає половину статів</p>}
              </div>
              {view === "info" && <div className="flex shrink-0 flex-col gap-1">{actions(piece, () => setView("choice"))}</div>}
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
                <ArtifactSetInfo itemKey={piece.itemKey} wornKeys={wornKeys} />
                <section className="space-y-1">
                  <h3 className="text-sm font-semibold">Майстерність коваля</h3>
                  <p className="rounded-lg bg-white/80 px-3 py-1.5 text-sm text-slate-600">
                    {piece.mastery} з {catalog.maxMastery}
                    {piece.experienceToNext != null && ` · до рівня ${piece.level + 1} — ${piece.experienceToNext} досвіду`}
                  </p>
                </section>
                {note}
                {!upgradable && <p className="text-xs text-amber-700">Предмет на ринку: прокачати можна, знявши лот.</p>}
              </>
            )}

            {view === "choice" && choice.render(piece, () => setView("info"))}

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
              <GameButton onClick={() => setView("mastery")} disabled={!upgradable}>
                Майстерність коваля
              </GameButton>
              <GameButton onClick={() => setView("feed")} variant="accent" disabled={!upgradable}>
                Посилити
              </GameButton>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
