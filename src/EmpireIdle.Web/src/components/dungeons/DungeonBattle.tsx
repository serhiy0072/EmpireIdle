import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { AbilityView, CombatantView, DungeonRunView, TurnLog } from "../../lib/apiTypes";
import { isApiError } from "../../lib/api";
import { fetchDungeonRun, useAbandonDungeonRun, useDungeonTurn } from "../../lib/queries/dungeons";
import ErrorBanner from "../ErrorBanner";
import CombatantCard from "./CombatantCard";

interface Props {
  playerId: string;
  run: DungeonRunView;
  onFinished: () => void;
}

/** Пауза між ходами автобою; ×2 ділить її навпіл. */
const TURN_DELAY_MS = 900;

/** Сплеск шкоди над бійцем гасне сам — без цього він лишався б до наступного ходу. */
const SPLASH_MS = 700;

type Splash = { damage: number; healed: number; critical: boolean };

/**
 * Підсумок забігу за станом із сервера. Нічия окрема від поразки: команда
 * жива, тож гравцеві треба пояснити, чому бій зупинено.
 */
type Outcome = { title: string; hint: string };

const LOST: Outcome = {
  title: "Команда полягла",
  hint: "Енергію витрачено. Підніміть рівні героїв або візьміть інший склад.",
};

const OUTCOMES: Record<string, Outcome> = {
  Won: { title: "Данж зачищено", hint: "Нагороду вже зараховано: ресурси в селі, артефакт в інвентарі." },
  Lost: LOST,
  // Бій покинули в іншій вкладці — ця дізналась про це лише на своєму ході
  Abandoned: { title: "Забіг покинуто", hint: "Енергію витрачено, нагороди немає." },
  TimedOut: {
    title: "Бій затягнувся",
    hint: "Хвилю не дограно за відведені раунди. Енергію витрачено. Візьміть склад із більшою шкодою.",
  },
};

/**
 * Покроковий бій.
 *
 * Стан бою живе на сервері: клієнт лише показує його й надсилає ходи.
 * Автобій — це той самий запит із auto=true, тож перемкнутися можна
 * будь-якої миті, не чекаючи кінця бою.
 */
export default function DungeonBattle({ playerId, run, onFinished }: Props) {
  const turn = useDungeonTurn(playerId);
  const abandon = useAbandonDungeonRun(playerId);

  const [state, setState] = useState<DungeonRunView>(run);
  const [auto, setAuto] = useState(true);
  const [fast, setFast] = useState(false);
  const [targetIndex, setTargetIndex] = useState<number | null>(null);
  const [splashes, setSplashes] = useState<Record<number, Splash>>({});
  const [log, setLog] = useState<string[]>([]);
  const [finished, setFinished] = useState<DungeonRunView | null>(null);

  // Свіже посилання для таймера автобою: інакше він ганяв би застарілий стан
  const busy = useRef(false);

  const combatants = state.combatants;
  const actor = state.actorIndex == null ? null : combatants[state.actorIndex];
  const heroTurn = actor?.side === "Heroes";
  const enemies = useMemo(() => combatants.filter((c) => c.side === "Enemies"), [combatants]);
  const heroes = useMemo(() => combatants.filter((c) => c.side === "Heroes"), [combatants]);

  const describe = useCallback(
    (entries: TurnLog[], people: CombatantView[]) => {
      // Хід несе лише ключ вміння — назву знають самі бійці
      const abilityNames = new Map(
        people.flatMap((person) => person.abilities.map((ability) => [ability.key, ability.displayName] as const)),
      );

      return entries.map((entry) => {
        const who = people[entry.actorIndex]?.displayName ?? "?";
        const what = entry.stunned
          ? "оглушений"
          : entry.abilityKey === null || entry.abilityKey === undefined
            ? "удар"
            : (abilityNames.get(entry.abilityKey) ?? entry.abilityKey);
        const effects = entry.effects
          .map((effect) => {
            const name = people[effect.targetIndex]?.displayName ?? "?";
            if ((effect.healed ?? 0) > 0) return `${name} +${Math.round(effect.healed ?? 0)}`;
            if ((effect.shieldGained ?? 0) > 0) return `${name} щит ${Math.round(effect.shieldGained ?? 0)}`;
            return `${name} −${Math.round(effect.damage ?? 0)}${effect.critical === true ? "!" : ""}${effect.died === true ? " †" : ""}`;
          })
          .join(", ");

        return `${who}: ${what}${effects === "" ? "" : ` → ${effects}`}`;
      });
    },
    [],
  );

  /** Один хід: власний або автоматичний. Відповідь несе новий стан — перезапит не потрібен. */
  const act = useCallback(
    async (isAuto: boolean, abilityKey?: string | null, targetIndex?: number | null) => {
      if (busy.current) return;
      busy.current = true;

      try {
        let result: DungeonRunView;

        try {
          result = await turn.mutateAsync({
            runId: state.runId,
            expectedTurn: state.turnNumber,
            auto: isAuto,
            abilityKey,
            targetIndex,
          });
        } catch (error: unknown) {
          // Сервер уже пішов далі (втрачена відповідь, друга вкладка) або дві
          // вкладки влучили в той самий момент: дію не застосовано, тож беремо
          // свіжий стан і граємо з нього — це не збій
          if (isApiError(error) && (error.is("StaleTurn") || error.is("ConcurrencyConflict"))) {
            turn.reset();
            const fresh = await fetchDungeonRun(playerId);

            if (fresh === null) onFinished();
            else setState(fresh);

            setTargetIndex(null);
            return;
          }

          // Інший збій (мережа, сервер): автобій зупиняємо явно, а не завмираємо
          // мовчки — банер покаже причину, гравець увімкне автобій, коли схоче.
          // Виняток не прокидаємо: його вже тримає turn.error
          setAuto(false);
          return;
        }

        setState(result);
        setTargetIndex(null);
        // Склад до ходу, а не після: коли відповідь уже перевела бій на
        // наступну хвилю, індекси ходів указують на попередній ростер
        setLog((previous) => [...describe(result.turns, state.combatants), ...previous].slice(0, 40));

        const fresh: Record<number, Splash> = {};
        for (const entry of result.turns)
          for (const effect of entry.effects)
            fresh[effect.targetIndex] = {
              damage: effect.damage ?? 0,
              healed: effect.healed ?? 0,
              critical: effect.critical === true,
            };

        setSplashes(fresh);
        window.setTimeout(() => setSplashes({}), SPLASH_MS);

        if (result.state !== "InProgress") {
          setFinished(result);
          setAuto(false);
        }
      } finally {
        busy.current = false;
      }
    },
    [describe, onFinished, playerId, state, turn],
  );

  // Автобій: наступний хід сам, поки гравець не перемкнув режим
  useEffect(() => {
    if (!auto || finished !== null || state.actorIndex == null) return;

    const timer = window.setTimeout(() => void act(true), fast ? TURN_DELAY_MS / 2 : TURN_DELAY_MS);

    return () => window.clearTimeout(timer);
  }, [auto, fast, finished, state, act]);

  /**
   * Хід ворога в ручному режимі теж робить сервер, але запитом без дії:
   * auto=true віддав би йому й ходи героїв, і ручне керування зникло б.
   */
  useEffect(() => {
    if (auto || finished !== null || state.actorIndex == null || heroTurn) return;

    const timer = window.setTimeout(() => void act(false), fast ? TURN_DELAY_MS / 2 : TURN_DELAY_MS);

    return () => window.clearTimeout(timer);
  }, [auto, fast, finished, heroTurn, state, act]);

  // Правило ліній: поки жива передня лінія, задню дістають лише вміння,
  // що її ігнорують. Провокація перебиває лінію: поки живий провокатор, бити
  // можна лише його. Рахуємо те саме, що й BattleEngine.CanTarget, аби не
  // пропонувати цілі, за які сервер однаково відмовить
  const frontAlive = useMemo(
    () => enemies.some((enemy) => enemy.line === "Front" && enemy.health > 0),
    [enemies],
  );

  const taunting = useMemo(
    () => enemies.filter((enemy) => enemy.health > 0 && enemy.statuses.some((status) => status.kind === "Taunt")),
    [enemies],
  );

  const reachable = useCallback(
    (combatant: CombatantView, ignoresLine: boolean) => {
      if (combatant.health <= 0) return false;
      if (ignoresLine) return true;
      if (taunting.length > 0) return taunting.some((enemy) => enemy.index === combatant.index);
      return !frontAlive || combatant.line === "Front";
    },
    [frontAlive, taunting],
  );

  const target = targetIndex === null ? null : (combatants[targetIndex] ?? null);

  /** Вміння, які саме зараз можуть чекати на ціль того чи того боку. */
  const readyAbilities = (actor?.abilities ?? []).filter((ability) => ability.ready);

  const canTarget = (combatant: CombatantView) => {
    if (auto || !heroTurn || finished !== null || combatant.health <= 0) return false;

    // Лікування й щити наводяться на своїх, атаки — на ворога
    if (combatant.side === "Heroes")
      return readyAbilities.some((ability) => ability.target === "SingleAlly");

    return (
      reachable(combatant, false) ||
      readyAbilities.some((ability) => ability.ignoresLine && ability.target === "SingleEnemy")
    );
  };

  /** Чи можна застосувати вміння до вже обраної цілі. */
  const canUse = (ability: AbilityView) => {
    if (!ability.ready) return false;

    if (ability.target === "SingleAlly") return target !== null && target.side === "Heroes" && target.health > 0;

    if (ability.target === "SingleEnemy")
      return target !== null && target.side === "Enemies" && reachable(target, ability.ignoresLine);

    return true;
  };

  const button = "rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50";

  if (finished !== null) {
    const won = finished.state === "Won";
    const outcome = OUTCOMES[finished.state] ?? LOST;
    const reward = finished.reward;

    return (
      <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-4">
        <h2 className={`text-lg font-medium ${won ? "text-emerald-700" : "text-rose-700"}`}>{outcome.title}</h2>
        <p className="text-sm text-slate-600">{outcome.hint}</p>

        {reward != null && (
          <div className="space-y-1 rounded-lg bg-slate-50 p-3 text-sm">
            <div className="text-slate-700">
              {reward.resources.map((line) => `${line.resource} +${line.amount.toLocaleString("uk-UA")}`).join(" · ")}
            </div>
            {reward.artifacts.map((artifact) => (
              <div key={artifact.itemKey} className="text-slate-800">
                🏺 {artifact.displayName}
              </div>
            ))}
          </div>
        )}
        <button
          type="button"
          onClick={onFinished}
          className="rounded-lg bg-slate-800 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-900"
        >
          До списку данжів
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm text-slate-600">
          Хвиля {state.wave} з {state.waveCount}
        </span>

        <div className="ml-auto flex items-center gap-1">
          <button type="button" onClick={() => setAuto((value) => !value)} className={button}>
            {auto ? "Автобій ✓" : "Автобій"}
          </button>
          <button type="button" onClick={() => setFast((value) => !value)} className={button}>
            {fast ? "×2 ✓" : "×2"}
          </button>
          <button
            type="button"
            onClick={() => abandon.mutate(state.runId, { onSuccess: onFinished })}
            disabled={abandon.isPending}
            className="rounded-lg border border-red-200 px-3 py-1 text-sm text-red-700 hover:bg-red-50 disabled:opacity-50"
          >
            Вийти
          </button>
        </div>
      </div>

      <ErrorBanner error={turn.error ?? abandon.error} />

      <div className="grid gap-3 sm:grid-cols-2">
        <section className="space-y-2">
          <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Команда</h3>
          {heroes.map((combatant) => (
            <CombatantCard
              key={combatant.index}
              combatant={combatant}
              active={state.actorIndex === combatant.index}
              targetable={canTarget(combatant)}
              selected={targetIndex === combatant.index}
              splash={splashes[combatant.index] ?? null}
              onSelect={() => setTargetIndex(combatant.index)}
            />
          ))}
        </section>

        <section className="space-y-2">
          <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Вороги</h3>
          {enemies.map((combatant) => (
            <CombatantCard
              key={combatant.index}
              combatant={combatant}
              active={state.actorIndex === combatant.index}
              targetable={canTarget(combatant)}
              selected={targetIndex === combatant.index}
              splash={splashes[combatant.index] ?? null}
              onSelect={() => setTargetIndex(combatant.index)}
            />
          ))}
        </section>
      </div>

      {!auto && heroTurn && actor !== null && (
        <div className="space-y-2 rounded-xl border border-amber-300 bg-amber-50 p-3">
          <p className="text-sm text-amber-900">
            Хід: <span className="font-medium">{actor.displayName}</span>
            {target === null ? " — оберіть ціль" : ""}
          </p>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => void act(false, null, targetIndex)}
              disabled={turn.isPending || target === null || target.side !== "Enemies" || !reachable(target, false)}
              className="rounded-lg bg-slate-800 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-900 disabled:opacity-50"
            >
              Удар
            </button>
            {actor.abilities.map((ability) => (
              <button
                key={ability.key}
                type="button"
                title={ability.description}
                onClick={() => void act(false, ability.key, targetIndex)}
                disabled={turn.isPending || !canUse(ability)}
                className="rounded-lg bg-violet-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-violet-700 disabled:opacity-50"
              >
                {ability.displayName} · {ability.energyCost}⚡
              </button>
            ))}
          </div>
        </div>
      )}

      {log.length > 0 && (
        <ol className="max-h-40 space-y-0.5 overflow-y-auto rounded-xl border border-slate-200 bg-white p-3 text-xs text-slate-600">
          {log.map((line, index) => (
            <li key={index}>{line}</li>
          ))}
        </ol>
      )}
    </div>
  );
}
