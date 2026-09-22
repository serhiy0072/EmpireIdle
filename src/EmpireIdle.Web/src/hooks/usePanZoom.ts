import { useCallback, useEffect, useRef, useState, type PointerEvent, type RefObject, type WheelEvent } from "react";

export interface Transform {
  x: number;
  y: number;
  k: number;
}

interface Bounds {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
}

/** Зсув, після якого жест вважається тяганням, а не тапом. */
const DRAG_THRESHOLD = 6;

/** Межі масштабу відносно «вся ділянка в кадрі» — типові, село їх не змінює. */
const DEFAULT_ZOOM = { min: 0.9, max: 6 };

export interface ZoomLimits {
  min: number;
  max: number;
}

function distance(a: { x: number; y: number }, b: { x: number; y: number }): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

/**
 * Панорамування й зум через трансформ групи SVG.
 *
 * Трансформ, а не viewBox: дельти вказівника в пікселях одразу стають
 * зсувом, без перерахунку одиниць. Тягання не плутається з тапом —
 * після зсуву понад поріг wasDragged() гасить клік по будівлі.
 */
export function usePanZoom(containerRef: RefObject<HTMLDivElement | null>, world: Bounds, limits: ZoomLimits = DEFAULT_ZOOM) {
  const [transform, setTransform] = useState<Transform | null>(null);
  const fitScale = useRef(1);
  const pointers = useRef(new Map<number, { x: number; y: number }>());
  const moved = useRef(0);
  const dragged = useRef(false);

  /** Трансформ, за якого bounds вписані в контейнер; null — контейнер ще без розміру. */
  const fitted = useCallback(
    (bounds: Bounds): Transform | null => {
      const rect = containerRef.current?.getBoundingClientRect();
      if (rect === undefined || rect.width === 0 || rect.height === 0) return null;

      const k = Math.min(rect.width / (bounds.maxX - bounds.minX), rect.height / (bounds.maxY - bounds.minY)) * 0.95;

      return {
        k,
        x: rect.width / 2 - ((bounds.minX + bounds.maxX) / 2) * k,
        y: rect.height / 2 - ((bounds.minY + bounds.maxY) / 2) * k,
      };
    },
    [containerRef],
  );

  // Перший показ: уся ділянка в кадрі. Далі розмір контейнера камеру не скидає
  useEffect(() => {
    const element = containerRef.current;
    if (element === null) return;

    const fit = () => {
      const next = fitted(world);
      if (next === null) return;

      fitScale.current = next.k;
      setTransform((current) => current ?? next);
    };

    fit();

    const observer = new ResizeObserver(fit);
    observer.observe(element);

    return () => observer.disconnect();
  }, [containerRef, world, fitted]);

  /** Повернути камеру на задані межі — «Додому» на світовій мапі. */
  const focus = useCallback(
    (bounds: Bounds) => {
      const next = fitted(bounds);
      if (next !== null) setTransform(next);
    },
    [fitted],
  );

  const zoomAt = useCallback(
    (px: number, py: number, factor: number) => {
      setTransform((t) => {
        if (t === null) return t;

        const k = Math.min(fitScale.current * limits.max, Math.max(fitScale.current * limits.min, t.k * factor));
        const ratio = k / t.k;

        // Точка під курсором лишається на місці
        return { k, x: px - (px - t.x) * ratio, y: py - (py - t.y) * ratio };
      });
    },
    [limits.max, limits.min],
  );

  const local = (event: { clientX: number; clientY: number }) => {
    const rect = containerRef.current?.getBoundingClientRect();

    return { x: event.clientX - (rect?.left ?? 0), y: event.clientY - (rect?.top ?? 0) };
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    pointers.current.set(event.pointerId, local(event));

    if (pointers.current.size === 1) {
      moved.current = 0;
      dragged.current = false;
    }
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const previous = pointers.current.get(event.pointerId);
    if (previous === undefined) return;

    const current = local(event);

    if (pointers.current.size === 1) {
      const dx = current.x - previous.x;
      const dy = current.y - previous.y;
      moved.current += Math.abs(dx) + Math.abs(dy);

      if (moved.current > DRAG_THRESHOLD) {
        // Захоплення лише після порогу: інакше тап не дійшов би до будівлі
        if (!dragged.current) {
          dragged.current = true;
          containerRef.current?.setPointerCapture(event.pointerId);
        }

        setTransform((t) => (t === null ? t : { ...t, x: t.x + dx, y: t.y + dy }));
      }
    } else if (pointers.current.size === 2) {
      const other = [...pointers.current.entries()].find(([id]) => id !== event.pointerId)?.[1];

      if (other !== undefined) {
        const before = distance(previous, other);
        const after = distance(current, other);

        if (before > 0) {
          zoomAt((current.x + other.x) / 2, (current.y + other.y) / 2, after / before);
        }

        dragged.current = true;
      }
    }

    pointers.current.set(event.pointerId, current);
  };

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    pointers.current.delete(event.pointerId);
  };

  const onWheel = (event: WheelEvent<HTMLDivElement>) => {
    const point = local(event);
    zoomAt(point.x, point.y, event.deltaY < 0 ? 1.1 : 1 / 1.1);
  };

  return {
    transform,
    handlers: { onPointerDown, onPointerMove, onPointerUp, onPointerCancel: onPointerUp, onWheel },
    wasDragged: () => dragged.current,
    focus,
  };
}
