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

/** Межі масштабу відносно «вся ділянка в кадрі». */
const MIN_ZOOM = 0.9;
const MAX_ZOOM = 6;

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
export function usePanZoom(containerRef: RefObject<HTMLDivElement | null>, world: Bounds) {
  const [transform, setTransform] = useState<Transform | null>(null);
  const fitScale = useRef(1);
  const pointers = useRef(new Map<number, { x: number; y: number }>());
  const moved = useRef(0);
  const dragged = useRef(false);

  // Перший показ: уся ділянка в кадрі. Далі розмір контейнера камеру не скидає
  useEffect(() => {
    const element = containerRef.current;
    if (element === null) return;

    const fit = () => {
      const { width, height } = element.getBoundingClientRect();
      if (width === 0 || height === 0) return;

      const k = Math.min(width / (world.maxX - world.minX), height / (world.maxY - world.minY)) * 0.95;
      fitScale.current = k;

      setTransform(
        (current) =>
          current ?? {
            k,
            x: width / 2 - ((world.minX + world.maxX) / 2) * k,
            y: height / 2 - ((world.minY + world.maxY) / 2) * k,
          },
      );
    };

    fit();

    const observer = new ResizeObserver(fit);
    observer.observe(element);

    return () => observer.disconnect();
  }, [containerRef, world]);

  const zoomAt = useCallback((px: number, py: number, factor: number) => {
    setTransform((t) => {
      if (t === null) return t;

      const k = Math.min(fitScale.current * MAX_ZOOM, Math.max(fitScale.current * MIN_ZOOM, t.k * factor));
      const ratio = k / t.k;

      // Точка під курсором лишається на місці
      return { k, x: px - (px - t.x) * ratio, y: py - (py - t.y) * ratio };
    });
  }, []);

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
  };
}
