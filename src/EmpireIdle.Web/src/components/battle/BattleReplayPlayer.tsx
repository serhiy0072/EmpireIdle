import { useEffect, useRef, useState, type ReactElement } from "react";
import type { BattleReplay } from "../../lib/apiTypes";
import { loadAutoSkills, loadSpeed, saveAutoSkills, saveSpeed } from "../../lib/battleSettings";
import { classLabel } from "../../lib/heroClasses";
import { useCatalog, type Catalog } from "../../lib/queries/catalog";
import { heroArtUrl } from "../heroes/heroArt";
import { GameButton } from "../game/GameUi";
import BattleHeroBar from "./BattleHeroBar";
import { advance, marchedLines, startPlayback, type Playback } from "./battleReplay";
import { t } from "i18next";

const HEIGHT = 320;
const LANES = 5;

/** Поле над першою й під останньою доріжкою: там піднімаються числа шкоди й висять смужки здоров'я. */
const PADDING = 24;

/** Відступ лінії від центру поля частками ширини: фронт лоб у лоб, за ним середня, далі тил. */
const LINE_OFFSETS = [0.07, 0.2, 0.33];

/** Скільки тиків число висить над бійцем. */
const FLOAT_TICKS = 8;

interface Spot {
  x: number;
  y: number;
}

/** Скільки частин ширини відділяє ворожу лінію line від ворожого фронту; дробова — посеред ходу. */
function lineShift(line: number): number {
  const at = (index: number) => LINE_OFFSETS[Math.min(index, LINE_OFFSETS.length - 1)] ?? 0;
  const whole = Math.floor(line);

  return at(whole) + (at(whole + 1) - at(whole)) * (line - whole) - at(0);
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

    return { x: heroX + side * (24 + Math.floor(order / 2) * 10), y: y + (order % 2 === 0 ? -7 : 7) };
  });
}

function draw(
  context: CanvasRenderingContext2D,
  width: number,
  replay: BattleReplay,
  playback: Playback,
  spots: Spot[],
  catalog: Catalog,
  icons: Map<number, HTMLImageElement>,
  bossLane: number | null,
) {
  context.clearRect(0, 0, width, HEIGHT);

  const ground = context.createLinearGradient(0, 0, width, 0);
  ground.addColorStop(0, "#1e3a5f");
  ground.addColorStop(0.5, "#3f3a2a");
  ground.addColorStop(1, "#5f1e2a");
  context.fillStyle = ground;
  context.fillRect(0, 0, width, HEIGHT);

  // Прорвали ворожу лінію — ідуть до наступної: зсув на відстань між ворожими лініями
  const position = (index: number): Spot | undefined => {
    const base = spots[index];
    const fighter = replay.fighters[index];
    if (base === undefined || fighter === undefined) return undefined;

    const lines = marchedLines(playback.marches.get(index), playback.tick);
    return { x: base.x + lineShift(lines) * width * (fighter.attacker ? 1 : -1), y: base.y };
  };

  replay.fighters.forEach((fighter, index) => {
    const spot = position(index);
    if (spot === undefined || playback.alive[index] !== true) return;

    const hero = fighter.leader == null;
    const boss = hero && !fighter.attacker && fighter.lane === bossLane;
    const radius = boss ? 21 : 16;
    const share = Math.max(0, (playback.health[index] ?? 0) / Math.max(1, fighter.maxHealth));
    const color = fighter.attacker ? (hero ? "#38bdf8" : "#7dd3fc") : hero ? "#f43f5e" : "#fda4af";

    context.fillStyle = color;
    context.strokeStyle = "#0f172a";
    context.lineWidth = 1.5;

    if (hero) {
      const icon = icons.get(index);
      context.beginPath();
      context.arc(spot.x, spot.y, radius, 0, Math.PI * 2);

      if (icon !== undefined && icon.complete && icon.naturalWidth > 0) {
        // Портрет героя в колі — той самий, що в картках: видно, хто це, а не лише клас
        context.save();
        context.clip();
        context.drawImage(icon, spot.x - radius, spot.y - radius, radius * 2, radius * 2);
        context.restore();
      } else {
        context.fill();
        context.fillStyle = "#0f172a";
        context.font = "bold 11px sans-serif";
        context.textAlign = "center";
        context.textBaseline = "middle";
        context.fillText(classLabel(catalog.hero(fighter.heroKey ?? "")?.class).slice(0, 2), spot.x, spot.y);
      }

      // Обвідка — колір сторони; бос — золота й товща: гравець одразу бачить, хто тут головний
      context.beginPath();
      context.arc(spot.x, spot.y, radius, 0, Math.PI * 2);
      context.strokeStyle = boss ? "#facc15" : color;
      context.lineWidth = boss ? 4 : 3;
      context.stroke();
      context.strokeStyle = "#0f172a";
      context.lineWidth = 1.5;
    } else {
      context.fillRect(spot.x - 4.5, spot.y - 4.5, 9, 9);
      context.strokeRect(spot.x - 4.5, spot.y - 4.5, 9, 9);
    }

    // Смужка здоров'я над бійцем
    const barWidth = boss ? 40 : hero ? 30 : 10;
    const barY = spot.y - (hero ? radius + 7 : 9);
    context.fillStyle = "#0f172a";
    context.fillRect(spot.x - barWidth / 2, barY, barWidth, 3);
    context.fillStyle = share > 0.5 ? "#4ade80" : share > 0.25 ? "#facc15" : "#ef4444";
    context.fillRect(spot.x - barWidth / 2, barY, barWidth * share, 3);
  });

  context.textAlign = "center";

  for (const floater of playback.floaters) {
    const age = playback.tick - floater.born;
    const spot = position(floater.fighter);
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
  /** Доріжка боса поверху — його фігурку видно здалеку; null — боса немає. */
  bossLane?: number | null;
  /**
   * Ручний бій: натискання готового героя — наказ на доріжку й тик, у відповідь — бій, перерахований
   * з усіма наказами. До тику наказу бій той самий, тож показ іде далі з того ж місця. null — авто.
   */
  onCast?: ((lane: number, tick: number) => Promise<BattleReplay>) | null;
  /** Відтворення скінчилось чи його пропустили — показати результат. */
  onFinished: () => void;
}

/**
 * Відтворення бою, порахованого сервером. Програвач нічого не вирішує: лише йде логом за часом,
 * ставить здоров'я з логу й малює. x2 і «Пропустити» міняють лише швидкість показу; «Авто» в ручному
 * бою натискає готові вміння саме, а вимкнене — чекає гравця.
 */
export default function BattleReplayPlayer({ replay: initial, bossLane = null, onCast = null, onFinished }: Props): ReactElement {
  const catalog = useCatalog();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const replayRef = useRef(initial);
  const playbackRef = useRef<Playback>(startPlayback(initial));
  // Поки сервер перераховує бій після наказу, показ стоїть: інакше він забіг би за тик наказу
  const pausedRef = useRef(false);
  const [replay, setReplay] = useState(initial);
  const [speed, setSpeed] = useState(loadSpeed);
  const speedRef = useRef(speed);
  const [auto, setAuto] = useState(loadAutoSkills);
  const autoRef = useRef(auto);
  const finishedRef = useRef(onFinished);
  const castRef = useRef(onCast);
  const orderRef = useRef<(hero: number) => void>(() => {});
  const [cards, setCards] = useState(() => ({
    health: initial.fighters.map((fighter) => fighter.maxHealth),
    tick: 0,
  }));

  const manual = onCast !== null;

  /** Наказ на наступний тик — події до нього вже показані, і перерахований бій до нього той самий. */
  const order = async (hero: number) => {
    const handler = castRef.current;
    const fighter = replayRef.current.fighters[hero];
    if (handler === null || fighter === undefined || pausedRef.current) return;

    pausedRef.current = true;

    try {
      const next = await handler(fighter.lane, Math.floor(playbackRef.current.tick) + 1);
      replayRef.current = next;
      setReplay(next);
    } catch {
      // Наказ не дійшов — бій іде далі, як був; помилку покаже сторінка. Авто вимикаємо лише на цей бій:
      // інакше кожен кадр слав би той самий наказ знову, поки не впреться в ліміт запитів
      autoRef.current = false;
      setAuto(false);
    } finally {
      pausedRef.current = false;
    }
  };

  useEffect(() => {
    finishedRef.current = onFinished;
    castRef.current = onCast;
    orderRef.current = (hero) => void order(hero);
  });

  useEffect(() => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (canvas == null || context == null) return;

    const ratio = window.devicePixelRatio || 1;
    const width = canvas.clientWidth;
    canvas.width = width * ratio;
    canvas.height = HEIGHT * ratio;
    context.scale(ratio, ratio);

    const spots = layout(replayRef.current, width);

    // Портрети героїв для поля — ті самі, що в картках; поки не завантажились, герой — коло з класом
    const icons = new Map<number, HTMLImageElement>();
    for (const fighter of replayRef.current.fighters) {
      if (fighter.leader != null) continue;

      const config = catalog.hero(fighter.heroKey ?? "");
      const icon = new Image();
      icon.src = heroArtUrl(fighter.heroKey ?? "", config?.class, config?.rank, config?.nativeTier ?? 1);
      icons.set(fighter.index, icon);
    }
    const playback = playbackRef.current;
    let last = performance.now();
    let lastCards = 0;
    let frame = 0;

    const step = (now: number) => {
      const elapsed = now - last;
      last = now;
      const current = replayRef.current;

      if (!pausedRef.current) advance(playback, current, Math.min(current.ticks, playback.tick + (elapsed / current.tickMilliseconds) * speedRef.current));
      draw(context, width, current, playback, spots, catalog, icons, bossLane);

      // Авто в ручному бою — готове вміння натискається саме, щойно настало
      const waiting = playback.ready.keys().next();
      if (castRef.current !== null && autoRef.current && !pausedRef.current && !waiting.done) orderRef.current(waiting.value);

      // Картки — React-станом, але не частіше за десять разів на секунду
      if (now - lastCards > 100) {
        lastCards = now;
        setCards({ health: [...playback.health], tick: playback.tick });
      }

      if (!pausedRef.current && playback.tick >= current.ticks && playback.next >= current.events.ticks.length) {
        finishedRef.current();
        return;
      }

      frame = requestAnimationFrame(step);
    };

    frame = requestAnimationFrame(step);

    return () => cancelAnimationFrame(frame);
  }, [catalog, bossLane]);

  const changeSpeed = () => {
    const next = speed === 1 ? 2 : 1;
    speedRef.current = next;
    setSpeed(next);
    saveSpeed(next);
  };

  const changeAuto = () => {
    autoRef.current = !auto;
    setAuto(!auto);
    saveAutoSkills(!auto);
  };

  const seconds = Math.floor((cards.tick * replay.tickMilliseconds) / 1000);

  return (
    <div className="space-y-2">
      <div className="relative">
        <canvas ref={canvasRef} className="block w-full rounded-xl ring-1 ring-white/20" style={{ height: HEIGHT }} />
        <p className="absolute bottom-2 left-2 rounded bg-black/50 px-2 py-0.5 text-xs">{t("battle:replay.seconds", { count: seconds })}</p>
      </div>

      <BattleHeroBar replay={replay} tick={cards.tick} health={cards.health} manual={manual && !auto} onCast={(hero) => void order(hero)} />

      <div className="flex justify-end gap-2">
        <GameButton variant={auto ? "accent" : "secondary"} onClick={changeAuto}>
          {t("battle:replay.auto")}
        </GameButton>
        <GameButton variant={speed === 2 ? "accent" : "secondary"} onClick={changeSpeed}>
          x2
        </GameButton>
        <GameButton variant="secondary" onClick={onFinished}>
          {t("battle:replay.skip")}
        </GameButton>
      </div>
    </div>
  );
}
