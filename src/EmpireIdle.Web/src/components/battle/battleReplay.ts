import type { BattleReplay } from "../../lib/apiTypes";

/** Види подій логу — ті самі числа, що BattleEventKind на сервері. */
export const EventKind = { Hit: 0, Death: 1, Heal: 2, Cast: 3, Shield: 4, Status: 5 } as const;

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
  floaters: Floater[];
}

export function startPlayback(replay: BattleReplay): Playback {
  return {
    tick: 0,
    next: 0,
    health: replay.fighters.map((fighter) => fighter.maxHealth),
    alive: replay.fighters.map(() => true),
    lastCast: new Map(),
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

    if (kind === EventKind.Cast) playback.lastCast.set(events.sources[i] ?? 0, tick);

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
