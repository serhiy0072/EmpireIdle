import { useEffect, useState } from "react";
import type { MarchIntent, MarchTargetType, SendMarchRequest } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { useGarrison } from "../../lib/queries/garrison";
import { useHeroes, type HeroSummary } from "../../lib/queries/heroes";
import { MARCH_INTENT, MARCH_TARGET, usePreviewMarch, useSendMarch } from "../../lib/queries/marches";
import { compactPower } from "../../lib/queries/rating";
import { useVillage } from "../../lib/queries/village";
import { isShieldActive } from "../../lib/shield";
import { formatDuration, parseTimeSpan } from "../../lib/time";
import { UnitHex } from "../army/CampPanels";
import ErrorBanner from "../ErrorBanner";
import { GameButton, RoleBadge, StarRow } from "../game/GameUi";
import HeroPortrait from "../heroes/HeroPortrait";
import { useHotkeys } from "../../hooks/useHotkeys";

interface Props {
  playerId: string;
  target: { type: MarchTargetType; id: string; name: string; intent?: MarchIntent };
  onSent: () => void;
  onCancel: () => void;
}

/** Прогноз бою одним рядком під героями, як на референсі. */
const VERDICT_LINES: Record<number, { text: string; className: string }> = {
  0: { text: "Цілком імовірно, що ви переможете", className: "text-emerald-300" },
  1: { text: "Перемога можлива, але не певна", className: "text-amber-300" },
  2: { text: "Цей бій не виграти", className: "text-rose-300" },
};

/**
 * Відправка армії (референс 19.10.2026): повноекранне вікно з ціллю, героями-картками, повзунками
 * військ і нижньою панеллю — співвідношення ролей, «Зняти всіх», «Порівну», «Максимум», час і «Відправити».
 * Кожен герой веде лише юнітів своєї ролі й не більше за свої конвої. Прогноз рахує сервер: клієнт
 * не знає ні пасивок, ні місцевості, ні щитів. Підкріплення не б'ється — прогнозу не показуємо.
 */
export default function SendMarchForm({ playerId, target, onSent, onCancel }: Props) {
  const catalog = useCatalog();
  // Героїв у марші щонайбільше (GDD §6.1) — по одному на роль; число з конфіга
  const maxHeroes = catalog.maxHeroesPerMarch;
  const garrison = useGarrison(playerId);
  const heroes = useHeroes(playerId);
  const preview = usePreviewMarch(playerId);
  const send = useSendMarch(playerId);
  const village = useVillage(playerId);

  const intent = target.intent ?? MARCH_INTENT.attack;
  const reinforce = intent === MARCH_INTENT.reinforce;
  const tame = intent === MARCH_INTENT.tame;

  // Щит після падіння знімає будь-який напад на гравця — попереджаємо до кліку
  const losesShield =
    (target.type === MARCH_TARGET.village || target.type === MARCH_TARGET.camp) && isShieldActive(village.data?.shieldUntil);

  // null — гравець ще не крутив повзунки: іде максимум, що влазить у конвої (рішення 21.10.2026)
  const [counts, setCounts] = useState<Record<string, number> | null>(null);
  const [picked, setPicked] = useState<string[] | null>(null);
  const [picking, setPicking] = useState(false);

  const idleHeroes = [...(heroes.data?.heroes ?? [])].filter((hero) => hero.state === "Idle").sort((a, b) => b.power - a.power);
  // Без вибору — до трьох найсильніших вільних, по одному на роль, як велить сервер (рішення 21.10.2026)
  const strongest = idleHeroes.filter(
    (hero, index) => idleHeroes.findIndex((other) => other.unitType === hero.unitType) === index,
  );
  const chosen = picked ?? strongest.slice(0, maxHeroes).map((hero) => hero.id);
  const leaders = chosen.flatMap((id) => idleHeroes.filter((hero) => hero.id === id));

  // Кожен герой веде лише юнітів своєї ролі й не більше за свої конвої
  const leaderOf = (unitType: string) => leaders.find((hero) => hero.unitType == null || hero.unitType === unitType) ?? null;
  // Старші рівні — першими: «Максимум» і «Порівну» беруть найсильніших
  const stacks = (garrison.data?.units ?? [])
    .filter((stack) => stack.count > 0 && leaderOf(stack.unitType) !== null)
    .sort((a, b) => a.unitType.localeCompare(b.unitType) || b.level - a.level);
  const keyOf = (stack: { unitType: string; level: number }) => `${stack.unitType}@${stack.level}`;

  const units: Record<string, number> = {};
  const room = new Map(leaders.map((hero) => [hero.id, hero.convoyCapacity]));

  for (const stack of stacks) {
    const leader = leaderOf(stack.unitType)!;
    const wanted = counts === null ? stack.count : Math.max(counts[keyOf(stack)] ?? 0, 0);
    const count = Math.min(wanted, stack.count, room.get(leader.id) ?? 0);

    if (count > 0) units[keyOf(stack)] = count;
    room.set(leader.id, (room.get(leader.id) ?? 0) - count);
  }

  const sent = Object.values(units).reduce((sum, count) => sum + count, 0);
  const capacity = leaders.reduce((sum, hero) => sum + hero.convoyCapacity, 0);
  const ready = sent > 0 && leaders.length > 0;

  /** Кожному герою — до quota його ролі, від старших рівнів. */
  const fill = (quota: (hero: HeroSummary) => number) => {
    const next: Record<string, number> = {};

    for (const hero of leaders) {
      let left = quota(hero);

      for (const stack of stacks.filter((candidate) => leaderOf(candidate.unitType)?.id === hero.id)) {
        const take = Math.min(stack.count, left);
        next[keyOf(stack)] = take;
        left -= take;
      }
    }

    setCounts(next);
  };

  const available = (hero: HeroSummary) =>
    Math.min(
      hero.convoyCapacity,
      stacks.filter((stack) => leaderOf(stack.unitType)?.id === hero.id).reduce((sum, stack) => sum + stack.count, 0),
    );

  const fillMax = () => fill(available);
  // Порівну: кожен стек ділиться на марші, які ще можна відправити, — решті походів лишиться
  // стільки ж (рішення 21.10.2026). Більше, ніж влізе в конвої, однаково не піде
  const fillEven = () => {
    const parts = Math.max(heroes.data?.marchesLeft ?? 1, 1);
    setCounts(Object.fromEntries(stacks.map((stack) => [keyOf(stack), Math.floor(stack.count / parts)])));
  };

  const request = (): SendMarchRequest => ({
    targetType: target.type,
    targetId: target.id,
    units,
    heroIds: leaders.map((hero) => hero.id),
    intent,
  });

  // Прев'ю рахується саме, щойно склад змінився: гравець бачить шанси, поки крутить повзунки.
  // Пауза — щоб не слати запит на кожен крок повзунка
  const previewKey = ready && !reinforce ? JSON.stringify(request()) : null;
  const { mutate: estimate } = preview;

  useEffect(() => {
    if (previewKey === null) return;

    const timer = window.setTimeout(() => estimate(JSON.parse(previewKey) as SendMarchRequest), 400);
    return () => window.clearTimeout(timer);
  }, [previewKey, estimate]);

  useHotkeys(
    {
      Escape: onCancel,
      Enter: () => {
        if (ready && !send.isPending) send.mutate(request(), { onSuccess: onSent });
      },
    },
    { modal: true },
  );

  const forecast = previewKey !== null && preview.data !== undefined ? preview.data : null;
  const verdict = forecast === null ? null : (VERDICT_LINES[forecast.verdict] ?? null);
  const title = reinforce ? "Підкріплення" : tame ? "Приручення" : "Ціль";

  // Співвідношення ролей у загоні — як унизу референса
  const shares = leaders.map((hero) => {
    const amount = stacks
      .filter((stack) => leaderOf(stack.unitType)?.id === hero.id)
      .reduce((sum, stack) => sum + (units[keyOf(stack)] ?? 0), 0);
    return { hero, share: sent === 0 ? 0 : Math.round((amount / sent) * 100) };
  });

  return (
    <div role="dialog" aria-modal="true" aria-label={`${title}: ${target.name}`} className="fixed inset-0 z-50 flex justify-center bg-slate-950/70">
      <div className="flex h-full w-full max-w-xl flex-col bg-gradient-to-b from-[#24467f] to-[#16305e] text-white shadow-2xl">
        <header className="flex items-center gap-3 px-4 py-3">
          <button type="button" aria-label="Назад" onClick={onCancel} className="text-3xl font-bold text-sky-200 hover:text-white">
            ←
          </button>
          <h2 className="min-w-0 flex-1 truncate text-xl font-semibold [text-shadow:0_1px_2px_#0008]">
            {title}: {target.name}
          </h2>
        </header>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto px-4 pb-4">
          <div className="flex items-center justify-between rounded-xl bg-[#1d3b6f] px-4 py-2 text-sm font-semibold ring-1 ring-[#335c9a]">
            <span title="Скільки військ іде з героями — з їхньої загальної місткості">
              👥 {sent.toLocaleString("uk-UA")}/{capacity.toLocaleString("uk-UA")}
            </span>
            {forecast?.attackerPower != null && (
              <span title={forecast.targetPower == null ? undefined : `Сила цілі ${compactPower(forecast.targetPower)}`}>
                ⚔ {Math.round(forecast.attackerPower).toLocaleString("uk-UA")}
              </span>
            )}
          </div>

          <ErrorBanner error={preview.error ?? send.error} />

          {reinforce && (
            <p className="rounded-lg bg-emerald-900/50 px-3 py-2 text-sm text-emerald-100 ring-1 ring-emerald-400/40">
              Військо стане гарнізоном споруди, а поки вона будується — прискорить будівництво: сильніший загін дає більше.
            </p>
          )}
          {tame && (
            <p className="rounded-lg bg-amber-900/50 px-3 py-2 text-sm text-amber-100 ring-1 ring-amber-400/40">
              Бій як звичайний. Перемога з шансом дасть звіра в звіринець замість здобичі; не вдасться — заберете здобич, а
              гарантія наблизиться.
            </p>
          )}
          {losesShield && (
            <p className="rounded-lg bg-amber-900/50 px-3 py-2 text-sm text-amber-100 ring-1 ring-amber-400/40">
              Напад на гравця зніме ваш щит після падіння міста.
            </p>
          )}

          <div className="grid grid-cols-3 gap-3">
            {Array.from({ length: maxHeroes }, (_, slot) => {
              const hero = leaders[slot];
              const config = hero === undefined ? null : catalog.hero(hero.heroKey);

              return hero === undefined ? (
                <button
                  key={`empty-${slot}`}
                  type="button"
                  onClick={() => setPicking(true)}
                  disabled={idleHeroes.length <= leaders.length}
                  className="flex aspect-[3/4] items-center justify-center rounded-xl bg-[#335c9a]/60 text-5xl font-bold text-white/90 ring-1 ring-[#5b85c4] hover:bg-[#335c9a] disabled:opacity-40"
                  aria-label="Додати героя"
                >
                  +
                </button>
              ) : (
                <div
                  key={hero.id}
                  className="relative flex aspect-[3/4] flex-col overflow-hidden rounded-xl bg-gradient-to-b from-amber-400 to-orange-600 ring-2 ring-amber-200"
                >
                  <button type="button" onClick={() => setPicking(true)} className="flex min-h-0 flex-1 items-center justify-center" title={catalog.heroName(hero.heroKey)}>
                    <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={96} />
                  </button>
                  <span className="absolute left-1 top-1">
                    <RoleBadge role={hero.unitType ?? undefined} size={22} />
                  </span>
                  <button
                    type="button"
                    aria-label="Прибрати героя"
                    onClick={() => setPicked(chosen.filter((id) => id !== hero.id))}
                    className="absolute right-1 top-1 h-7 w-7 rounded-full bg-rose-500 text-lg font-bold leading-6 text-white ring-2 ring-white hover:bg-rose-400"
                  >
                    −
                  </button>
                  <div className="bg-black/30 px-2 py-1">
                    <p className="text-sm font-bold [text-shadow:0_1px_1px_#000]">Рів. {hero.effectiveLevel}</p>
                    <StarRow starParts={hero.starParts} partsPerStar={catalog.partsPerStar} maxStars={catalog.maxStars} size={12} />
                  </div>
                </div>
              );
            })}
          </div>

          {verdict !== null && (
            <p className={`text-center text-base font-semibold ${verdict.className} ${preview.isPending ? "opacity-60" : ""}`}>
              {verdict.text}
              {forecast?.verdict === 1 && ` · шанс ${Math.round(forecast.winChance * 100)}%`}
            </p>
          )}

          {leaders.length === 0 ? (
            <p className="rounded-xl bg-[#1d3b6f] px-4 py-6 text-center text-sm text-sky-100 ring-1 ring-[#335c9a]">
              {idleHeroes.length === 0 ? "Немає вільного героя: кожен похід веде герой, що вдома." : "Додайте героя: військо йде лише з героєм."}
            </p>
          ) : stacks.length === 0 ? (
            <p className="rounded-xl bg-[#1d3b6f] px-4 py-6 text-center text-sm text-sky-100 ring-1 ring-[#335c9a]">
              У гарнізоні немає юнітів ролей обраних героїв — кожен герой веде лише своїх.
            </p>
          ) : (
            <div className="space-y-2">
              {stacks.map((stack) => {
                const key = keyOf(stack);
                const value = units[key] ?? 0;
                // Повзунок дістає до того, що влізе в конвої героя разом із поточним значенням
                const max = Math.min(stack.count, value + (room.get(leaderOf(stack.unitType)?.id ?? "") ?? 0));
                // Перший дотик фіксує поточний максимум, щоб решта повзунків не обнулилась
                const set = (next: number) => setCounts((prev) => ({ ...(prev ?? units), [key]: Math.max(0, Math.min(max, next)) }));

                return (
                  <div key={key} className="flex items-center gap-3 rounded-xl bg-sky-50 px-3 py-2 text-slate-800 ring-1 ring-sky-200">
                    <UnitHex unitType={stack.unitType} level={stack.level} size={52} />
                    <div className="min-w-0 flex-1 space-y-1.5">
                      <div className="flex items-center justify-between gap-2">
                        <span className="truncate text-sm text-slate-600">
                          {catalog.unitName(stack.unitType)} · рів. {stack.level}
                        </span>
                        <span className="flex items-center gap-1 text-sm font-semibold">
                          <input
                            type="number"
                            min={0}
                            max={max}
                            value={value}
                            onChange={(event) => set(Number(event.target.value))}
                            aria-label={`Скільки: ${catalog.unitName(stack.unitType)}`}
                            className="w-20 rounded-lg bg-slate-200 px-2 py-0.5 text-center tabular-nums"
                          />
                          <span className="text-slate-500">/{stack.count.toLocaleString("uk-UA")}</span>
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <button
                          type="button"
                          onClick={() => set(value - 1)}
                          className="h-8 w-8 shrink-0 rounded-lg bg-gradient-to-b from-sky-400 to-blue-600 text-xl font-bold leading-none text-white shadow"
                          aria-label="Менше"
                        >
                          −
                        </button>
                        <input
                          type="range"
                          min={0}
                          max={stack.count}
                          value={value}
                          onChange={(event) => set(Number(event.target.value))}
                          className="h-2 min-w-0 flex-1 cursor-pointer accent-emerald-500"
                          aria-label={`Повзунок: ${catalog.unitName(stack.unitType)}`}
                        />
                        <button
                          type="button"
                          onClick={() => set(value + 1)}
                          className="h-8 w-8 shrink-0 rounded-lg bg-gradient-to-b from-sky-400 to-blue-600 text-xl font-bold leading-none text-white shadow"
                          aria-label="Більше"
                        >
                          +
                        </button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {shares.length > 0 && (
          <div className="flex items-center justify-around border-t border-[#335c9a] bg-[#16305e] px-4 py-2 text-sm">
            <span className="text-sky-200">Співвідношення військ:</span>
            {shares.map(({ hero, share }) => (
              <span key={hero.id} className="flex items-center gap-1 font-semibold">
                <RoleBadge role={hero.unitType ?? undefined} size={20} />
                {share}%
              </span>
            ))}
          </div>
        )}

        <footer className="flex items-center gap-2 border-t-2 border-[#5b85c4] bg-gradient-to-b from-[#1d3b6f] to-[#0f2147] px-3 py-3">
          <GameButton onClick={() => setCounts({})} variant="secondary" disabled={sent === 0}>
            Зняти всіх
          </GameButton>
          <GameButton onClick={fillEven} variant="secondary" disabled={stacks.length === 0}>
            Порівну
          </GameButton>
          <GameButton onClick={fillMax} variant="secondary" disabled={stacks.length === 0}>
            Максимум
          </GameButton>
          <div className="ml-auto flex flex-col items-center gap-1">
            {forecast !== null && <span className="font-mono text-xs text-sky-100">⏱ {formatDuration(parseTimeSpan(forecast.travelTime))}</span>}
            <span data-tutorial="send-march">
              <GameButton onClick={() => send.mutate(request(), { onSuccess: onSent })} disabled={!ready || send.isPending}>
                {send.isPending ? "Відправляємо…" : "Відправити"}
              </GameButton>
            </span>
          </div>
        </footer>
      </div>

      {picking && (
        <HeroPicker
          heroes={idleHeroes}
          chosen={chosen}
          onAssign={(next) => {
            setPicked(next);
            setPicking(false);
          }}
          onClose={() => setPicking(false)}
        />
      )}
    </div>
  );
}

/**
 * «Обрати героїв» (референс 19.10.2026): угорі три місця, нижче — вільні герої від найсильнішого.
 * Тап додає чи прибирає; герой ролі, що вже зайнята, неактивний — у марші по одному на роль.
 */
function HeroPicker({
  heroes,
  chosen,
  onAssign,
  onClose,
}: {
  heroes: HeroSummary[];
  chosen: string[];
  onAssign: (next: string[]) => void;
  onClose: () => void;
}) {
  const catalog = useCatalog();
  const maxHeroes = catalog.maxHeroesPerMarch;
  const [draft, setDraft] = useState(chosen);

  useHotkeys({ Escape: onClose, Enter: () => onAssign(draft) }, { modal: true });

  const selected = draft.flatMap((id) => heroes.filter((hero) => hero.id === id));
  const roles = new Set(selected.map((hero) => hero.unitType));
  const toggle = (hero: HeroSummary) => setDraft(draft.includes(hero.id) ? draft.filter((id) => id !== hero.id) : [...draft, hero.id]);

  return (
    <div onClick={onClose} className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/60 p-4">
      <div
        onClick={(event) => event.stopPropagation()}
        className="flex max-h-[85vh] w-full max-w-md flex-col overflow-hidden rounded-2xl bg-sky-50 text-slate-800 shadow-2xl ring-2 ring-sky-300"
      >
        <div className="flex items-center bg-gradient-to-b from-sky-500 to-sky-600 px-4 py-2 text-white">
          <h3 className="flex-1 text-center text-lg font-semibold">Обрати героїв</h3>
          <button type="button" aria-label="Закрити" onClick={onClose} className="text-xl font-bold hover:text-sky-100">
            ✕
          </button>
        </div>

        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
          <div className="grid grid-cols-3 gap-3">
            {Array.from({ length: maxHeroes }, (_, slot) => {
              const hero = selected[slot];
              const config = hero === undefined ? null : catalog.hero(hero.heroKey);

              return hero === undefined ? (
                <div key={`empty-${slot}`} className="flex aspect-[3/4] items-center justify-center rounded-xl bg-sky-200/70 text-4xl text-white ring-2 ring-amber-300">
                  +
                </div>
              ) : (
                <button
                  key={hero.id}
                  type="button"
                  onClick={() => toggle(hero)}
                  className="relative flex aspect-[3/4] flex-col items-center justify-end overflow-hidden rounded-xl bg-gradient-to-b from-amber-400 to-orange-600 pb-1 text-white ring-2 ring-amber-200"
                >
                  <span className="absolute left-1 top-1">
                    <RoleBadge role={hero.unitType ?? undefined} size={20} />
                  </span>
                  <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={72} />
                  <span className="text-xs font-bold [text-shadow:0_1px_1px_#000]">Рів. {hero.effectiveLevel}</span>
                </button>
              );
            })}
          </div>

          <p className="text-xs text-slate-500">Від найсильнішого · по одному герою на роль</p>
          <div className="grid grid-cols-4 gap-2">
            {heroes.map((hero) => {
              const config = catalog.hero(hero.heroKey);
              const on = draft.includes(hero.id);
              const blocked = !on && (draft.length >= maxHeroes || roles.has(hero.unitType));

              return (
                <button
                  key={hero.id}
                  type="button"
                  onClick={() => toggle(hero)}
                  disabled={blocked}
                  title={`${catalog.heroName(hero.heroKey)} · ⚔ ${compactPower(hero.power)}${hero.isDefender ? " · боронить село" : ""}`}
                  className={`relative flex flex-col items-center rounded-lg bg-white p-1 ring-2 disabled:opacity-40 ${
                    on ? "ring-amber-400" : "ring-sky-200 hover:ring-sky-400"
                  }`}
                >
                  <span className="absolute left-0.5 top-0.5">
                    <RoleBadge role={hero.unitType ?? undefined} size={16} />
                  </span>
                  <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={52} />
                  <span className="text-[11px] font-semibold">Рів. {hero.effectiveLevel}</span>
                  <StarRow starParts={hero.starParts} partsPerStar={catalog.partsPerStar} maxStars={catalog.maxStars} size={9} />
                </button>
              );
            })}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3 border-t border-sky-200 p-4">
          <GameButton onClick={() => setDraft([])} variant="secondary" disabled={draft.length === 0}>
            Очистити
          </GameButton>
          <GameButton onClick={() => onAssign(draft)}>Призначити</GameButton>
        </div>
      </div>
    </div>
  );
}
