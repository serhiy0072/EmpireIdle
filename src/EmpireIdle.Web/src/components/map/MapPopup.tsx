import { useLayoutEffect, useRef, useState, type ReactNode } from "react";

interface Props {
  /** Точка в пікселях контейнера мапи, біля якої стоїть вікно: центр обраної клітини. */
  anchor: { x: number; y: number };
  children: ReactNode;
}

interface Measure {
  width: number;
  height: number;
  boundsWidth: number;
  boundsHeight: number;
}

/** Відступ від краю мапи й від самої клітини. */
const MARGIN = 8;
const GAP = 28;

/**
 * Вікно дій над клітиною: стоїть над нею (знизу, якщо згори не вміщається), не виходить за край мапи
 * й їде разом із мапою під час перетягування. Розміри міряємо після рендеру — вміст різний
 * для села, монстра й табору.
 */
export default function MapPopup({ anchor, children }: Props) {
  const ref = useRef<HTMLDivElement>(null);
  const [measure, setMeasure] = useState<Measure | null>(null);

  useLayoutEffect(() => {
    const element = ref.current;
    const parent = element?.parentElement ?? null;
    if (element === null || parent === null) return;

    const update = () =>
      setMeasure({
        width: element.offsetWidth,
        height: element.offsetHeight,
        boundsWidth: parent.clientWidth,
        boundsHeight: parent.clientHeight,
      });
    const observer = new ResizeObserver(update);
    observer.observe(element);
    observer.observe(parent);

    // Колесо над вікном гортає його вміст, а не зумить мапу (зум слухає нативну подію на контейнері)
    const keepWheel = (event: WheelEvent) => event.stopPropagation();
    element.addEventListener("wheel", keepWheel);

    return () => {
      observer.disconnect();
      element.removeEventListener("wheel", keepWheel);
    };
  }, []);

  const clamp = (value: number, max: number) => Math.min(Math.max(value, MARGIN), Math.max(MARGIN, max - MARGIN));

  const width = measure?.width ?? 0;
  const height = measure?.height ?? 0;
  const above = anchor.y - GAP - height;
  const top = above >= MARGIN ? above : anchor.y + GAP;

  return (
    <div
      ref={ref}
      className="pointer-events-auto absolute z-20 max-h-[calc(100%-1rem)] w-[min(20rem,calc(100%-1rem))] overflow-y-auto rounded-xl bg-white/95 shadow-2xl ring-2 ring-[#5b85c4]"
      // Поки розмір невідомий — невидиме, щоб не блимнути в куті
      style={{
        left: clamp(anchor.x - width / 2, (measure?.boundsWidth ?? 0) - width),
        top: clamp(top, (measure?.boundsHeight ?? 0) - height),
        visibility: measure === null ? "hidden" : "visible",
      }}
      // Перетягування мапи починається з натискання — над вікном воно не потрібне
      onPointerDown={(event) => event.stopPropagation()}
    >
      {children}
    </div>
  );
}
