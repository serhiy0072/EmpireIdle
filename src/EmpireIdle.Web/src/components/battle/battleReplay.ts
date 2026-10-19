import type { BattleReplay } from "../../lib/apiTypes";

/** Види подій логу — ті самі числа, що BattleEventKind на сервері. */
export const EventKind = { Hit: 0, Death: 1, Heal: 2, Cast: 3, Shield: 4, Status: 5, Advance: 6, Ready: 7 } as const;

/** Біти HitFlags на сервері. */
export const HitFlag = { Advantage: 1, Disadvantage: 2, Crit: 4, Blocked: 8, Skill: 16, Poison: 32 } as const;

/** Число, що спливає над бійцем: шкода, крит чи лікування. */
export interface Floater {
  fighter: number;
  text: string;
  color: string;
  big: boolean;
  /** Тик, на якому з'явилось — від нього рахується підйом і згасання. */
  born: number;
}

/** Перехід бійця між ворожими лініями — номери ліній, до яких він стоїть лицем, і тики ходу. */
export interface March {
  from: number;
  to: number;
  start: number;
  end: number;
}

/**
 * Стан відтворення на поточний тик. Тримається поза React: кадр малюється 60 разів на секунду,
 * а перерендер компонента на кожен кадр поклав би сторінку.
 */
export interface Playback {
  tick: number;
  next: number;
  health: number[];
  alive: boolean[];
  /** Тик останнього вміння героя — для спалаху картки. */
  lastCast: Map<number, number>;
  /** Ручний бій: герой → номер готового вміння, що чекає натискання. */
  ready: Map<number, number>;
  /** Ближній боєць іде до наступної ворожої лінії: з якої на яку і в які тики. */
  marches: Map<number, March>;
  floaters: Floater[];
}

export function startPlayback(replay: BattleReplay): Playback {
  return {
    tick: 0,
    next: 0,
    health: replay.fighters.map((fighter) => fighter.maxHealth),
    alive: replay.fighters.map(() => true),
    lastCast: new Map(),
    ready: new Map(),
    marches: new Map(),
    floaters: [],
  };
}

/** Скільки чисел може висіти водночас: у масовому бою їх тисячі, і екран перетворився б на кашу. */
const MAX_FLOATERS = 40;

/**
 * Застосовує події до заданого тику включно. Здоров'я ставиться на значення з логу, а не рахується:
 * частину ударів поглинає щит, і лише сервер знає скільки.
 */
export function advance(playback: Playback, replay: BattleReplay, toTick: number): void {
  const events = replay.events;
  const heroes = (index: number) => replay.fighters[index]?.leader == null;

  while (playback.next < events.ticks.length && (events.ticks[playback.next] ?? Infinity) <= toTick) {
    const i = playback.next++;
    const kind = events.kinds[i];
    const target = events.targets[i] ?? 0;
    const amount = events.amounts[i] ?? 0;
    const flags = events.flags[i] ?? 0;
    const tick = events.ticks[i] ?? 0;

    if (kind === EventKind.Hit || kind === EventKind.Heal) playback.health[target] = events.healths[i] ?? 0;

    if (kind === EventKind.Death) playback.alive[target] = false;

    const source = events.sources[i] ?? 0;
    const detail = events.details[i] ?? 0;

    if (kind === EventKind.Cast) {
      playback.lastCast.set(source, tick);
      if (playback.ready.get(source) === detail) playback.ready.delete(source);
    }

    if (kind === EventKind.Ready) playback.ready.set(source, detail);

    if (kind === EventKind.Advance)
      playback.marches.set(source, {
        from: playback.marches.get(source)?.to ?? 0,
        to: detail,
        start: tick,
        end: tick + (amount * 1000) / replay.tickMilliseconds,
      });

    // Числа — лише там, де вони щось кажуть: удари по героях, крити й лікування
    const crit = (flags & HitFlag.Crit) !== 0;
    const poison = (flags & HitFlag.Poison) !== 0;

    if (kind === EventKind.Hit && (crit || heroes(target)) && amount > 0)
      playback.floaters.push({
        fighter: target,
        text: Math.round(amount).toLocaleString("uk-UA"),
        color: poison ? "#c084fc" : crit ? "#facc15" : "#fca5a5",
        big: crit,
        born: tick,
      });

    if (kind === EventKind.Heal && heroes(target))
      playback.floaters.push({ fighter: target, text: `+${Math.round(amount)}`, color: "#86efac", big: false, born: tick });
  }

  if (playback.floaters.length > MAX_FLOATERS) playback.floaters.splice(0, playback.floaters.length - MAX_FLOATERS);
  playback.tick = toTick;
}

/** Відлік уміння героя на тику: скільки тиків лишилось і з якого повного відліку; ready — чекає натискання. */
export interface SkillClock {
  remaining: number;
  total: number;
  ready: boolean;
}

/** Тики, на яких уміння героя спрацювало (Cast) чи стало готовим у ручному бою (Ready), — по зростанню. */
export function skillTimeline(replay: BattleReplay, hero: number, skill: number): { tick: number; ready: boolean }[] {
  const events = replay.events;
  const timeline: { tick: number; ready: boolean }[] = [];

  events.kinds.forEach((kind, i) => {
    if ((kind === EventKind.Cast || kind === EventKind.Ready) && events.sources[i] === hero && events.details[i] === skill)
      timeline.push({ tick: events.ticks[i] ?? 0, ready: kind === EventKind.Ready });
  });

  return timeline;
}

/**
 * Відлік на тику з лінії часу вміння: від попереднього спрацювання (чи старту бою) до наступного
 * спрацювання чи готовності. null — до кінця бою вміння більше не спрацює.
 */
export function skillClock(timeline: { tick: number; ready: boolean }[], tick: number): SkillClock | null {
  let start = 0;
  // Готовність без спрацювання після неї — уміння чекає натискання
  let waiting = false;

  for (const mark of timeline) {
    if (mark.tick > tick) return waiting ? { remaining: 0, total: 1, ready: true } : { remaining: mark.tick - tick, total: Math.max(1, mark.tick - start), ready: false };

    waiting = mark.ready;
    if (!mark.ready) start = mark.tick;
  }

  return waiting ? { remaining: 0, total: 1, ready: true } : null;
}

/** Скільки ворожих ліній боєць пройшов на тику — дробове число посеред ходу. */
export function marchedLines(march: March | undefined, tick: number): number {
  if (march === undefined) return 0;
  if (tick >= march.end) return march.to;

  const share = Math.max(0, (tick - march.start) / Math.max(1, march.end - march.start));
  return march.from + (march.to - march.from) * share;
}
