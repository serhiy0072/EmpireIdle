import { useEffect, useRef } from "react";

/** Клавіші, на які гра відповідає (рішення 21.10.2026): Esc — назад, Enter — прийняти, стрілки — гортати. */
export type HotkeyName = "Escape" | "Enter" | "ArrowLeft" | "ArrowRight";

export type Hotkeys = Partial<Record<HotkeyName, () => void>>;

/**
 * Пріоритет шару в межах одного вікна: сусіди (герой ↔ герой) важливіші за вкладки,
 * вкладки — за загальне «Esc — назад» оболонки.
 */
export const HOTKEY_PRIORITY = { shell: -1, tabs: 0, neighbours: 1 } as const;

interface Options {
  /** Вікно поверх екрана: клавіші не провалюються під нього, навіть якщо вікно їх не знає. */
  modal?: boolean;
  priority?: number;
  enabled?: boolean;
}

interface Layer {
  keys: { current: Hotkeys };
  modal: boolean;
  priority: number;
}

const layers: Layer[] = [];

/** Друк у полі — не команда: тут стрілки рухають курсор, а Enter належить формі. */
function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;

  return target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName);
}

function onKeyDown(event: KeyboardEvent) {
  if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return;

  const name = event.key as HotkeyName;
  if (name !== "Escape" && name !== "Enter" && name !== "ArrowLeft" && name !== "ArrowRight") return;

  // Esc працює й з поля: інакше вікно з повзунком чи числом не закрити з клавіатури
  if (name !== "Escape" && isTyping(event.target)) return;

  // Enter на кнопці під фокусом уже натисне її сам — друге «прийняти» було б подвійною дією
  if (name === "Enter" && (event.repeat || event.target instanceof HTMLButtonElement)) return;

  // Верхнє вікно відрізає все, що під ним; усередині вікна вирішує пріоритет, потім свіжість шару
  let floor = 0;
  for (let i = layers.length - 1; i >= 0; i--) {
    if (layers[i]?.modal === true) {
      floor = i;
      break;
    }
  }

  const candidates = layers
    .slice(floor)
    .map((layer, index) => ({ layer, index }))
    .sort((a, b) => b.layer.priority - a.layer.priority || b.index - a.index);

  for (const { layer } of candidates) {
    const handler = layer.keys.current[name];
    if (handler === undefined) continue;

    event.preventDefault();
    handler();
    return;
  }
}

/**
 * Реакція екрана чи вікна на клавіші. Шари складаються в стек за порядком монтування:
 * відкрите вікно (modal) перехоплює клавіші, закрите — повертає їх екрану під ним.
 * Обробники читаються з ref, тож нова функція на кожен рендер не перереєстровує шар.
 */
export function useHotkeys(keys: Hotkeys, { modal = false, priority = 0, enabled = true }: Options = {}) {
  const ref = useRef(keys);

  useEffect(() => {
    ref.current = keys;
  });

  useEffect(() => {
    if (!enabled) return;

    const layer: Layer = { keys: ref, modal, priority };

    if (layers.length === 0) window.addEventListener("keydown", onKeyDown);
    layers.push(layer);

    return () => {
      layers.splice(layers.indexOf(layer), 1);
      if (layers.length === 0) window.removeEventListener("keydown", onKeyDown);
    };
  }, [enabled, modal, priority]);
}

/** Сусідній елемент списку по колу: стрілки гортають вкладки, героїв і звірів однаково. */
export function cycle<T>(items: readonly T[], current: T, step: 1 | -1): T {
  const index = items.indexOf(current);

  return items[(index + step + items.length) % items.length] ?? current;
}

/** Стрілки перемикають вкладки екрана по колу (рішення 21.10.2026). */
export function useTabHotkeys<T>(keys: readonly T[], value: T, onChange: (next: T) => void) {
  useHotkeys(
    { ArrowLeft: () => onChange(cycle(keys, value, -1)), ArrowRight: () => onChange(cycle(keys, value, 1)) },
    { priority: HOTKEY_PRIORITY.tabs, enabled: keys.length > 1 },
  );
}
