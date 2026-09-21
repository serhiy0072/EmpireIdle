import { useEffect, useState } from "react";

export interface TargetRect {
  id: string;
  top: number;
  left: number;
  width: number;
  height: number;
}

/** Крок опитування DOM: ціль може з'явитись після кліку або зсунутись від зуму мапи. */
const POLL_MS = 150;

function find(ids: string[]): TargetRect | null {
  for (const id of ids) {
    const element = document.querySelector<HTMLElement | SVGElement>(`[data-tutorial="${CSS.escape(id)}"]`);

    if (element === null) continue;

    const rect = element.getBoundingClientRect();

    // Елемент є, але не має розміру (display:none, порожня група) — шукаємо далі
    if (rect.width === 0 && rect.height === 0) continue;

    return { id, top: rect.top, left: rect.left, width: rect.width, height: rect.height };
  }

  return null;
}

/**
 * Прямокутник першої з цілей, що є на екрані. Опитування, а не
 * MutationObserver: ціль може рухатись без змін DOM (пан/зум SVG),
 * а 150 мс для підсвітки непомітно.
 */
export function useTargetRect(ids: string[]): TargetRect | null {
  const [rect, setRect] = useState<TargetRect | null>(null);
  const key = ids.join("|");

  useEffect(() => {
    const list = key === "" ? [] : key.split("|");

    const tick = () => {
      const next = find(list);

      setRect((current) => {
        if (current === null || next === null) return next === current ? current : next;

        const same =
          current.id === next.id &&
          Math.abs(current.top - next.top) < 0.5 &&
          Math.abs(current.left - next.left) < 0.5 &&
          Math.abs(current.width - next.width) < 0.5 &&
          Math.abs(current.height - next.height) < 0.5;

        return same ? current : next;
      });
    };

    tick();

    const timer = window.setInterval(tick, POLL_MS);
    window.addEventListener("resize", tick);
    window.addEventListener("scroll", tick, true);

    return () => {
      window.clearInterval(timer);
      window.removeEventListener("resize", tick);
      window.removeEventListener("scroll", tick, true);
    };
  }, [key]);

  return rect;
}
