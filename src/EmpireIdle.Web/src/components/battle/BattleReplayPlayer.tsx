import { useEffect, useRef, useState, type ReactElement } from "react";
import type { BattleReplay } from "../../lib/apiTypes";
import { classLabel } from "../../lib/heroClasses";
import { useCatalog, type Catalog } from "../../lib/queries/catalog";
import { GameButton } from "../game/GameUi";
import HeroPortrait from "../heroes/HeroPortrait";
import { advance, startPlayback, type Playback } from "./battleReplay";

const HEIGHT = 320;
const LANES = 5;

/** Поле над першою й під останньою доріжкою: там піднімаються числа шкоди й висять смужки здоров'я. */
const PADDING = 24;

/** Відступ лінії від центру поля частками ширини: фронт лоб у лоб, за ним середня, далі тил. */
const LINE_OFFSETS = [0.07, 0.2, 0.33];

/** Скільки тиків число висить над бійцем. */
const FLOAT_TICKS = 8;

/** Скільки тиків картка героя світиться після вміння. */
const CAST_TICKS = 6;

interface Spot {
  x: number;
  y: number;
}

/**
 * Місця бійців на полі: нападник ліворуч, захисник праворуч, доріжка — рядок. Глибину дає лінія
 * з логу — сервер знає, хто де стоїть. Герой стоїть на своїй лінії, його конвої — купкою позаду.
 */
function layout(replay: BattleReplay, width: number): Spot[] {
  const row = (HEIGHT - PADDING * 2) / LANES;
  const convoyIndex = new Map<number, number>();

  return replay.fighters.map((fighter) => {
    const side = fighter.attacker ? -1 : 1;
    const offset = LINE_OFFSETS[Math.min(fighter.line, LINE_OFFSETS.length - 1)] ?? 0;
    const heroX = width / 2 + side * width * offset;
    const y = PADDING + row * (fighter.lane + 0.5);

    if (fighter.leader == null) return { x: heroX, y };

    const order = convoyIndex.get(fighter.leader) ?? 0;
    convoyIndex.set(fighter.leader, order + 1);

    return { x: heroX + side * (18 + Math.floor(order / 2) * 10), y: y + (order % 2 === 0 ? -7 : 7) };
  });
}

function draw(context: CanvasRenderingContext2D, width: number, replay: BattleReplay, playback: Playback, spots: Spot[], catalog: Catalog) {
  context.clearRect(0, 0, width, HEIGHT);

  const ground = context.createLinearGradient(0, 0, width, 0);
  ground.addColorStop(0, "#1e3a5f");
  ground.addColorStop(0.5, "#3f3a2a");
  ground.addColorStop(1, "#5f1e2a");
  context.fillStyle = ground;
  context.fillRect(0, 0, width, HEIGHT);

  replay.fighters.forEach((fighter, index) => {
    const spot = spots[index];
    if (spot === undefined || playback.alive[index] !== true) return;

    const hero = fighter.leader == null;
    const share = Math.max(0, (playback.health[index] ?? 0) / Math.max(1, fighter.maxHealth));
    const color = fighter.attacker ? (hero ? "#38bdf8" : "#7dd3fc") : hero ? "#f43f5e" : "#fda4af";

    context.fillStyle = color;
    context.strokeStyle = "#0f172a";
    context.lineWidth = 1.5;

    if (hero) {
      context.beginPath();
      context.arc(spot.x, spot.y, 13, 0, Math.PI * 2);
      context.fill();
      context.stroke();
      context.fillStyle = "#0f172a";
      context.font = "bold 11px sans-serif";
      context.textAlign = "center";
      context.textBaseline = "middle";
      context.fillText(classLabel(catalog.hero(fighter.heroKey ?? "")?.class).slice(0, 2), spot.x, spot.y);
    } else {
      context.fillRect(spot.x - 4.5, spot.y - 4.5, 9, 9);
      context.strokeRect(spot.x - 4.5, spot.y - 4.5, 9, 9);
    }

    // Смужка здоров'я над бійцем
    const barWidth = hero ? 30 : 10;
    const barY = spot.y - (hero ? 20 : 9);
    context.fillStyle = "#0f172a";
    context.fillRect(spot.x - barWidth / 2, barY, barWidth, 3);
    context.fillStyle = share > 0.5 ? "#4ade80" : share > 0.25 ? "#facc15" : "#ef4444";
    context.fillRect(spot.x - barWidth / 2, barY, barWidth * share, 3);
  });

  context.textAlign = "center";

  for (const floater of playback.floaters) {
    const age = playback.tick - floater.born;
    const spot = spots[floater.fighter];
    if (spot === undefined || age < 0 || age > FLOAT_TICKS) continue;

    context.globalAlpha = 1 - age / FLOAT_TICKS;
    context.fillStyle = floater.color;
    context.font = `bold ${floater.big ? 15 : 11}px sans-serif`;
    context.fillText(floater.text, spot.x, spot.y - 24 - age * 2);
  }

  context.globalAlpha = 1;
}

interface Props {
  replay: BattleReplay;
  /** Відтворення скінчилось чи його пропустили — показати результат. */
  onFinished: () => void;
}

/**
 * Відтворення бою, порахованого сервером. Програвач нічого не вирішує: лише йде логом за часом,
 * ставить здоров'я з логу й малює. x2 і «Пропустити» міняють лише швидкість показу.
 */
export default function BattleReplayPlayer({ replay, onFinished }: Props): ReactElement {
  const catalog = useCatalog();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const playbackRef = useRef<Playback>(startPlayback(replay));
  const speedRef = useRef(1);
  const finishedRef = useRef(onFinished);
  const [speed, setSpeed] = useState(1);
  const [cards, setCards] = useState(() => ({
    health: replay.fighters.map((fighter) => fighter.maxHealth),
    tick: 0,
    casts: new Map<number, number>(),
  }));

  useEffect(() => {
    finishedRef.current = onFinished;
  }, [onFinished]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (canvas == null || context == null) return;

    const ratio = window.devicePixelRatio || 1;
    const width = canvas.clientWidth;
    canvas.width = width * ratio;
    canvas.height = HEIGHT * ratio;
    context.scale(ratio, ratio);

    const spots = layout(replay, width);
    const playback = playbackRef.current;
    let last = performance.now();
    let lastCards = 0;
    let frame = 0;

    const step = (now: number) => {
      const elapsed = now - last;
      last = now;

      advance(playback, replay, Math.min(replay.ticks, playback.tick + (elapsed / replay.tickMilliseconds) * speedRef.current));
      draw(context, width, replay, playback, spots, catalog);

      // Картки — React-станом, але не частіше за десять разів на секунду
      if (now - lastCards > 100) {
        lastCards = now;
        setCards({ health: [...playback.health], tick: playback.tick, casts: new Map(playback.lastCast) });
      }

      if (playback.tick >= replay.ticks && playback.next >= replay.events.ticks.length) {
        finishedRef.current();
        return;
      }

      frame = requestAnimationFrame(step);
    };

    frame = requestAnimationFrame(step);

    return () => cancelAnimationFrame(frame);
  }, [replay, catalog]);

  const changeSpeed = () => {
    const next = speed === 1 ? 2 : 1;
    speedRef.current = next;
    setSpeed(next);
  };

  const heroes = replay.fighters.filter((fighter) => fighter.attacker && fighter.leader == null);
  const seconds = Math.floor((cards.tick * replay.tickMilliseconds) / 1000);

  return (
    <div className="space-y-2">
      <div className="relative">
        <canvas ref={canvasRef} className="block w-full rounded-xl ring-1 ring-white/20" style={{ height: HEIGHT }} />
        <p className="absolute bottom-2 left-2 rounded bg-black/50 px-2 py-0.5 text-xs">{seconds} с</p>
      </div>

      <ol className="grid grid-cols-5 gap-2">
        {heroes.map((fighter) => {
          const config = catalog.hero(fighter.heroKey ?? "");
          const share = Math.max(0, (cards.health[fighter.index] ?? 0) / Math.max(1, fighter.maxHealth));
          const cast = cards.casts.get(fighter.index);
          const casting = cast !== undefined && cards.tick - cast < CAST_TICKS;

          return (
            <li
              key={fighter.index}
              className={`flex flex-col items-center gap-1 rounded-lg bg-white/5 p-1 text-[11px] ${share <= 0 ? "opacity-40 grayscale" : ""} ${casting ? "ring-2 ring-amber-300" : ""}`}
            >
              <HeroPortrait heroKey={fighter.heroKey ?? ""} heroClass={config?.class} rank={config?.rank} tier={config?.nativeTier ?? 1} size={36} />
              <span className="w-full truncate text-center">{catalog.heroName(fighter.heroKey ?? "")}</span>
              <span className="h-1.5 w-full rounded bg-slate-900">
                <span className="block h-full rounded bg-emerald-400" style={{ width: `${share * 100}%` }} />
              </span>
            </li>
          );
        })}
      </ol>

      <div className="flex justify-end gap-2">
        <GameButton variant="secondary" onClick={changeSpeed}>
          x{speed === 1 ? 2 : 1}
        </GameButton>
        <GameButton variant="secondary" onClick={onFinished}>
          Пропустити
        </GameButton>
      </div>
    </div>
  );
}
