import { useEffect, useRef } from "react";
import { BEAST_SCALE, beastArtUrl, type BeastFrame } from "./beastArt";
import { t } from "i18next";

export interface PenBeast {
  beastKey: string;
  level: number;
}

interface Props {
  beasts: PenBeast[];
  onSelect: (beastKey: string) => void;
}

/** Звір у загоні: де стоїть ногами, куди йде і скільки ще відпочиває. */
interface Walker {
  key: string;
  x: number;
  y: number;
  tx: number;
  ty: number;
  /** Скільки мс ще стоїть, перш ніж обрати нову ціль. */
  rest: number;
  facing: 1 | -1;
}

const HEIGHT = 340;
/** Смуга паркану згори; звірі ходять нижче неї. */
const FENCE_TOP = 64;
const MARGIN = 48;
/** Швидкість ходи, px/с. */
const SPEED = 34;
/** Звір 120×90 у SVG — у загоні трохи менший. */
const BASE = 0.85;
/** Як часто звір переставляє лапи, мс. */
const STEP_MS = 200;

/** Нова точка в межах загону: звірі не заходять за паркан. */
function randomSpot(width: number): { x: number; y: number } {
  return {
    x: MARGIN + Math.random() * Math.max(width - MARGIN * 2, 1),
    y: FENCE_TOP + 70 + Math.random() * (HEIGHT - FENCE_TOP - 90),
  };
}

/** Тло загону: трава, витоптана стежка, паркан згори й з боків, корито з водою й миска з м'ясом. */
function drawGround(ctx: CanvasRenderingContext2D, width: number) {
  const grass = ctx.createLinearGradient(0, 0, 0, HEIGHT);
  grass.addColorStop(0, "#a7c97f");
  grass.addColorStop(1, "#7fae5c");
  ctx.fillStyle = grass;
  ctx.fillRect(0, 0, width, HEIGHT);

  ctx.fillStyle = "rgba(146, 108, 64, 0.25)";
  ctx.beginPath();
  ctx.ellipse(width / 2, HEIGHT * 0.62, width * 0.38, HEIGHT * 0.22, 0, 0, Math.PI * 2);
  ctx.fill();

  // Кущики трави — сталі, щоб тло не мерехтіло між кадрами
  ctx.strokeStyle = "#5f8f3e";
  ctx.lineWidth = 1.5;
  for (let i = 0; i < Math.floor(width / 18); i++) {
    const x = (i * 97) % width;
    const y = FENCE_TOP + 20 + ((i * 53) % (HEIGHT - FENCE_TOP - 30));
    ctx.beginPath();
    ctx.moveTo(x - 3, y);
    ctx.lineTo(x - 5, y - 6);
    ctx.moveTo(x, y);
    ctx.lineTo(x, y - 8);
    ctx.moveTo(x + 3, y);
    ctx.lineTo(x + 5, y - 6);
    ctx.stroke();
  }

  // Паркан: дві жердини й стовпчики
  ctx.fillStyle = "#8b5a2b";
  ctx.strokeStyle = "#4a2c17";
  ctx.lineWidth = 1.5;
  for (const y of [FENCE_TOP - 34, FENCE_TOP - 16]) {
    ctx.fillRect(0, y, width, 6);
    ctx.strokeRect(0, y, width, 6);
  }
  for (let x = 10; x < width; x += 44) {
    ctx.fillRect(x, FENCE_TOP - 46, 10, 46);
    ctx.strokeRect(x, FENCE_TOP - 46, 10, 46);
  }

  // Корито з водою зліва й миска з м'ясом справа
  ctx.fillStyle = "#7c4a2b";
  ctx.fillRect(16, FENCE_TOP + 6, 70, 18);
  ctx.fillStyle = "#38bdf8";
  ctx.fillRect(21, FENCE_TOP + 9, 60, 8);
  ctx.strokeRect(16, FENCE_TOP + 6, 70, 18);

  ctx.fillStyle = "#57534e";
  ctx.beginPath();
  ctx.ellipse(width - 50, FENCE_TOP + 18, 20, 8, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#f87171";
  ctx.beginPath();
  ctx.ellipse(width - 50, FENCE_TOP + 14, 12, 5, 0, 0, Math.PI * 2);
  ctx.fill();
}

/**
 * Загін звірів (референс 21.10.2026): приручені звірі бродять полем, під кожним — «Ур. N»;
 * натиск на звіра відкриває його екран. Хода — лише візуал: позиції не зберігаються й не впливають на гру.
 * Хто просить менше руху (prefers-reduced-motion), бачить звірів, що стоять.
 */
export default function BeastEnclosure({ beasts, onSelect }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const walkersRef = useRef<Walker[]>([]);
  const beastsRef = useRef(beasts);
  const selectRef = useRef(onSelect);

  useEffect(() => {
    beastsRef.current = beasts;
    selectRef.current = onSelect;
  });

  useEffect(() => {
    const canvas = canvasRef.current;
    const ctx = canvas?.getContext("2d");
    if (canvas === null || ctx === null || ctx === undefined) return;

    const still = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const images = new Map<string, HTMLImageElement>();
    const image = (key: string, frame: BeastFrame) => {
      const id = `${key}:${frame}`;
      let img = images.get(id);
      if (img === undefined) {
        img = new Image();
        img.src = beastArtUrl(key, frame);
        images.set(id, img);
      }
      return img;
    };

    let width = canvas.clientWidth;
    const resize = () => {
      width = canvas.clientWidth;
      const ratio = window.devicePixelRatio || 1;
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(HEIGHT * ratio);
      ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(canvas);

    /** Звірі в загоні — ті, що є в даних: нові з'являються в довільній точці, відпущені зникають. */
    const sync = () => {
      const keys = new Set(beastsRef.current.map((beast) => beast.beastKey));
      const known = new Set(walkersRef.current.map((walker) => walker.key));
      walkersRef.current = walkersRef.current.filter((walker) => keys.has(walker.key));

      for (const beast of beastsRef.current) {
        if (known.has(beast.beastKey)) continue;
        const spot = randomSpot(width);
        walkersRef.current.push({ key: beast.beastKey, ...spot, tx: spot.x, ty: spot.y, rest: Math.random() * 2000, facing: 1 });
      }
    };

    const size = (key: string) => {
      const scale = BASE * (BEAST_SCALE[key] ?? 1);
      return { w: 120 * scale, h: 90 * scale };
    };

    let last = performance.now();
    let frameId = 0;

    const tick = (now: number) => {
      const dt = Math.min(now - last, 100);
      last = now;
      sync();

      drawGround(ctx, width);

      const walkers = walkersRef.current;
      for (const walker of walkers) {
        // Загін звузився (поворот екрана) — звір повертається всередину
        walker.x = Math.min(Math.max(walker.x, MARGIN), Math.max(width - MARGIN, MARGIN));
        if (still) continue;

        if (walker.rest > 0) {
          walker.rest -= dt;
          if (walker.rest <= 0) {
            const spot = randomSpot(width);
            walker.tx = spot.x;
            walker.ty = spot.y;
          }
          continue;
        }

        const dx = walker.tx - walker.x;
        const dy = walker.ty - walker.y;
        const distance = Math.hypot(dx, dy);
        const travel = (SPEED * dt) / 1000;

        if (distance <= travel) {
          walker.x = walker.tx;
          walker.y = walker.ty;
          walker.rest = 1500 + Math.random() * 3500;
        } else {
          walker.x += (dx / distance) * travel;
          walker.y += (dy / distance) * travel;
          if (Math.abs(dx) > 1) walker.facing = dx > 0 ? 1 : -1;
        }
      }

      // Хто нижче на екрані — той ближче: малюємо згори донизу
      const levels = new Map(beastsRef.current.map((beast) => [beast.beastKey, beast.level]));
      for (const walker of [...walkers].sort((a, b) => a.y - b.y)) {
        const { w, h } = size(walker.key);
        const walking = !still && walker.rest <= 0;
        const frame: BeastFrame = walking ? (Math.floor(now / STEP_MS) % 2 === 0 ? 1 : -1) : 0;
        const bob = walking ? Math.abs(Math.sin(now / STEP_MS)) * 2 : 0;
        const img = image(walker.key, frame);

        ctx.fillStyle = "rgba(0, 0, 0, 0.22)";
        ctx.beginPath();
        ctx.ellipse(walker.x, walker.y, w * 0.36, 6, 0, 0, Math.PI * 2);
        ctx.fill();

        if (img.complete && img.naturalWidth > 0) {
          ctx.save();
          ctx.translate(walker.x, walker.y - bob);
          ctx.scale(walker.facing, 1);
          // Лапи в SVG стоять на y = 84 з 90 — ставимо звіра ними на точку
          ctx.drawImage(img, -w / 2, -h * (84 / 90), w, h);
          ctx.restore();
        }

        const label = t("heroes:list.level", { level: levels.get(walker.key) ?? 1 });
        ctx.font = "bold 13px system-ui, sans-serif";
        ctx.textAlign = "center";
        ctx.lineWidth = 3;
        ctx.strokeStyle = "rgba(15, 33, 71, 0.85)";
        ctx.strokeText(label, walker.x, walker.y + 18);
        ctx.fillStyle = "#ffffff";
        ctx.fillText(label, walker.x, walker.y + 18);
      }

      frameId = requestAnimationFrame(tick);
    };
    frameId = requestAnimationFrame(tick);

    /** Найближчий до глядача звір під курсором: натиск і курсор-рука. */
    const hit = (event: MouseEvent): Walker | null => {
      const rect = canvas.getBoundingClientRect();
      const x = event.clientX - rect.left;
      const y = event.clientY - rect.top;

      return (
        [...walkersRef.current]
          .sort((a, b) => b.y - a.y)
          .find((walker) => {
            const { w, h } = size(walker.key);
            return Math.abs(x - walker.x) <= w * 0.45 && y <= walker.y + 22 && y >= walker.y - h * 0.9;
          }) ?? null
      );
    };
    const onClick = (event: MouseEvent) => {
      const walker = hit(event);
      if (walker !== null) selectRef.current(walker.key);
    };
    const onMove = (event: MouseEvent) => {
      canvas.style.cursor = hit(event) === null ? "default" : "pointer";
    };
    canvas.addEventListener("click", onClick);
    canvas.addEventListener("mousemove", onMove);

    return () => {
      cancelAnimationFrame(frameId);
      observer.disconnect();
      canvas.removeEventListener("click", onClick);
      canvas.removeEventListener("mousemove", onMove);
    };
  }, []);

  return (
    <canvas
      ref={canvasRef}
      style={{ height: HEIGHT }}
      className="block w-full rounded-2xl shadow-inner ring-2 ring-[#5b85c4]"
      aria-label={t("village:beasts.enclosure")}
    />
  );
}
