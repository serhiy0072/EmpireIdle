import type { CSSProperties } from "react";
import type { TutorialStep } from "./steps";
import { useTargetRect, type TargetRect } from "./useTargetRect";

interface Props {
  step: TutorialStep;
  onDismiss: () => void;
  onSkip: () => void;
}

/** Запас навколо цілі, щоб рамка не різала кнопку по краю. */
const PAD = 6;
const CARD_WIDTH = 320;
const GAP = 12;

/** Картка під ціллю, якщо є місце, інакше над нею; без цілі — внизу по центру. */
function cardStyle(rect: TargetRect | null): CSSProperties {
  if (rect === null) {
    return { left: "50%", bottom: 24, transform: "translateX(-50%)", width: CARD_WIDTH };
  }

  const below = rect.top + rect.height + GAP;
  const fitsBelow = below + 180 < window.innerHeight;
  const left = Math.min(Math.max(rect.left + rect.width / 2 - CARD_WIDTH / 2, 12), window.innerWidth - CARD_WIDTH - 12);

  return fitsBelow
    ? { left, top: below, width: CARD_WIDTH }
    : { left, bottom: window.innerHeight - rect.top + GAP, width: CARD_WIDTH };
}

/**
 * Спотлайт і картка кроку. У режимі block чотири затемнені смуги навколо
 * цілі перехоплюють кліки, а дірка між ними — ні: гравець може натиснути
 * лише те, що показуємо. У guide затемнення немає — лише пульсуюча рамка.
 */
export default function TutorialOverlay({ step, onDismiss, onSkip }: Props) {
  const rect = useTargetRect(step.targets);
  const spotlight = step.mode !== "hint" && rect !== null;
  // Крок без цілей (привітання) блокує все; ціль є, але не знайдена — не блокуємо нічого:
  // краще слабша підказка, ніж замкнений екран
  const modal = step.mode === "block" && step.targets.length === 0;
  const blocking = step.mode === "block" && rect !== null;
  // Дія обов'язкова лише в block-кроці з ціллю; решту можна сховати кнопкою
  const dismissable = step.mode !== "block" || step.done === undefined;
  const dismissLabel = step.mode === "hint" ? "Зрозуміло" : step.mode === "guide" ? "Приховати" : "Далі";

  const hole = rect === null
    ? null
    : { top: rect.top - PAD, left: rect.left - PAD, width: rect.width + PAD * 2, height: rect.height + PAD * 2 };

  return (
    <div className="pointer-events-none fixed inset-0 z-50">
      {blocking && hole !== null && (
        <>
          <div className="pointer-events-auto absolute inset-x-0 top-0 bg-slate-900/50" style={{ height: Math.max(0, hole.top) }} />
          <div
            className="pointer-events-auto absolute inset-x-0 bottom-0 bg-slate-900/50"
            style={{ top: hole.top + hole.height }}
          />
          <div
            className="pointer-events-auto absolute left-0 bg-slate-900/50"
            style={{ top: hole.top, height: hole.height, width: Math.max(0, hole.left) }}
          />
          <div
            className="pointer-events-auto absolute right-0 bg-slate-900/50"
            style={{ top: hole.top, height: hole.height, left: hole.left + hole.width }}
          />
        </>
      )}

      {modal && <div className="pointer-events-auto absolute inset-0 bg-slate-900/50" />}

      {spotlight && hole !== null && (
        <div
          className="absolute rounded-xl ring-4 ring-amber-400 animate-pulse"
          style={{ top: hole.top, left: hole.left, width: hole.width, height: hole.height }}
        />
      )}

      <div
        role="dialog"
        aria-label={step.title}
        className="pointer-events-auto absolute space-y-2 rounded-xl border border-amber-300 bg-white p-4 shadow-xl"
        style={cardStyle(spotlight ? rect : null)}
      >
        <h3 className="font-medium text-slate-800">{step.title}</h3>
        <p className="text-sm text-slate-600">{step.text}</p>

        <div className="flex items-center justify-between gap-2 pt-1">
          {step.mode === "hint" ? (
            <span />
          ) : (
            <button type="button" onClick={onSkip} className="text-xs text-slate-500 hover:underline">
              Пропустити навчання
            </button>
          )}

          {dismissable && (
            <button
              type="button"
              onClick={onDismiss}
              className="rounded-lg bg-amber-500 px-3 py-1 text-sm font-medium text-white hover:bg-amber-600"
            >
              {dismissLabel}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
