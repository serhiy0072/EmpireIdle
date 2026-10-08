import { useState } from "react";
import type { MarchIntent, MarchTargetType, SendMarchRequest } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { useGarrison } from "../../lib/queries/garrison";
import { useHeroes } from "../../lib/queries/heroes";
import { BATTLE_ODDS, MARCH_INTENT, MARCH_TARGET, usePreviewMarch, useSendMarch } from "../../lib/queries/marches";
import { useVillage } from "../../lib/queries/village";
import { isShieldActive } from "../../lib/shield";
import { formatDuration, parseTimeSpan } from "../../lib/time";
import ErrorBanner from "../ErrorBanner";

interface Props {
  playerId: string;
  target: { type: MarchTargetType; id: string; name: string; intent?: MarchIntent };
  onSent: () => void;
  onCancel: () => void;
}

/**
 * Відправка армії: склад із гарнізону, герой (обов'язковий — він і є слот
 * маршу), оцінка шансів до кліку. Прев'ю рахує сервер: клієнт не знає
 * ні пасивок, ні місцевості, ні щитів. Підкріплення (своя споруда клану)
 * не б'ється: шансів не оцінюємо, лише відправляємо.
 */
export default function SendMarchForm({ playerId, target, onSent, onCancel }: Props) {
  const catalog = useCatalog();
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

  const [counts, setCounts] = useState<Record<string, number>>({});
  const [picked, setPicked] = useState<string[] | null>(null);

  const idleHeroes = (heroes.data?.heroes ?? []).filter((hero) => hero.state === "Idle");
  // До трьох героїв різних ролей (GDD §6.1); без вибору — перший вільний
  const chosen = picked ?? (idleHeroes[0] === undefined ? [] : [idleHeroes[0].id]);
  const leaders = idleHeroes.filter((hero) => chosen.includes(hero.id));
  const takenRoles = new Set(leaders.map((hero) => hero.unitType));

  const toggleHero = (heroId: string) =>
    setPicked(chosen.includes(heroId) ? chosen.filter((id) => id !== heroId) : [...chosen, heroId]);

  // Кожен герой веде лише юнітів своєї ролі й не більше за свої конвої
  const leaderOf = (unitType: string) =>
    leaders.find((hero) => hero.unitType == null || hero.unitType === unitType) ?? null;
  const stacks = (garrison.data?.units ?? []).filter((stack) => stack.count > 0 && leaderOf(stack.unitType) !== null);

  const units: Record<string, number> = {};
  const room = new Map(leaders.map((hero) => [hero.id, hero.convoyCapacity]));

  for (const stack of stacks) {
    const key = `${stack.unitType}@${stack.level}`;
    const leader = leaderOf(stack.unitType)!;
    const count = Math.min(Math.max(counts[key] ?? 0, 0), stack.count, room.get(leader.id) ?? 0);

    if (count > 0) units[key] = count;
    room.set(leader.id, (room.get(leader.id) ?? 0) - count);
  }

  const sentBy = (heroId: string) =>
    stacks
      .filter((stack) => leaderOf(stack.unitType)?.id === heroId)
      .reduce((sum, stack) => sum + (units[`${stack.unitType}@${stack.level}`] ?? 0), 0);

  const ready = Object.keys(units).length > 0 && leaders.length > 0;

  const request = (): SendMarchRequest => ({
    targetType: target.type,
    targetId: target.id,
    units,
    heroIds: leaders.map((hero) => hero.id),
    intent,
  });

  const odds = preview.data === undefined ? null : BATTLE_ODDS[preview.data.odds];

  return (
    <div className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="font-medium text-slate-800">
          {reinforce ? "Підкріплення" : tame ? "Приручення:" : "Похід на"} {target.name}
        </h3>
        <button type="button" onClick={onCancel} className="text-sm text-slate-500 hover:underline">
          Скасувати
        </button>
      </div>

      <ErrorBanner error={preview.error ?? send.error} />

      {reinforce && (
        <p className="rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          Військо стане гарнізоном споруди, а поки вона будується — прискорить будівництво: сильніший загін дає більше.
        </p>
      )}

      {tame && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Бій як звичайний. Перемога з шансом дасть звіра в звіринець замість здобичі; не вдасться — заберете здобич, а
          гарантія наблизиться.
        </p>
      )}

      {losesShield && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Напад на гравця зніме ваш щит після падіння міста.
        </p>
      )}

      {stacks.length === 0 ? (
        <p className="text-sm text-slate-500">
          {leaders.length > 0
            ? "У гарнізоні немає юнітів ролей обраних героїв — кожен герой веде лише своїх."
            : "Оберіть героя: військо йде лише з героєм."}
        </p>
      ) : (
        <div className="space-y-2">
          <p className="text-xs font-medium text-slate-500">Хто йде</p>
          <ul className="space-y-0.5 text-xs text-slate-500">
            {leaders.map((hero) => (
              <li key={hero.id}>
                {catalog.heroName(hero.heroKey)}: {hero.unitType != null ? catalog.unitName(hero.unitType) : "військо"}{" "}
                {sentBy(hero.id)} з {hero.convoyCapacity}
              </li>
            ))}
          </ul>
          {stacks.map((stack) => {
            const key = `${stack.unitType}@${stack.level}`;
            const value = units[key] ?? 0;
            const left = room.get(leaderOf(stack.unitType)?.id ?? "") ?? 0;

            return (
              <div key={key} className="flex items-center justify-between gap-2 text-sm">
                <span className="text-slate-700">
                  {catalog.unitName(stack.unitType)} (рів. {stack.level}) · є {stack.count}
                </span>
                <div className="flex items-center gap-1">
                  <input
                    type="number"
                    min={0}
                    max={stack.count}
                    value={value}
                    onChange={(event) => setCounts((prev) => ({ ...prev, [key]: Number(event.target.value) }))}
                    className="w-20 rounded-lg border border-slate-300 px-2 py-1 text-right"
                  />
                  <button
                    type="button"
                    onClick={() => setCounts((prev) => ({ ...prev, [key]: Math.min(stack.count, value + left) }))}
                    className="rounded-lg border border-slate-300 px-2 py-1 text-xs text-slate-600 hover:bg-slate-50"
                  >
                    усі
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <div className="space-y-1">
        <p className="text-xs font-medium text-slate-500">Герої на чолі — до 3, різних ролей</p>
        {idleHeroes.length === 0 ? (
          <p className="text-sm text-slate-500">Немає вільного героя: кожен похід веде герой, що вдома.</p>
        ) : (
          <div className="flex flex-wrap gap-1">
            {idleHeroes.map((hero) => {
              const selected = chosen.includes(hero.id);
              const blocked = !selected && (leaders.length >= 3 || takenRoles.has(hero.unitType));

              return (
                <button
                  key={hero.id}
                  type="button"
                  onClick={() => toggleHero(hero.id)}
                  disabled={blocked}
                  className={`rounded-lg border px-2 py-1 text-xs ${
                    selected ? "border-emerald-500 bg-emerald-50 text-emerald-800" : "border-slate-300 text-slate-700 hover:bg-slate-50"
                  } disabled:opacity-40`}
                >
                  {catalog.heroName(hero.heroKey)} · рів. {hero.effectiveLevel}
                  {hero.unitType != null && ` · ${catalog.unitName(hero.unitType)} до ${hero.convoyCapacity}`}
                  {hero.isLeader ? " · лідер" : ""}
                </button>
              );
            })}
          </div>
        )}
      </div>

      {preview.data !== undefined && odds !== null && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className={`rounded px-2 py-0.5 ${odds.className}`}>{odds.label}</span>
          <span className="text-slate-600">в дорозі {formatDuration(parseTimeSpan(preview.data.travelTime))}</span>
        </div>
      )}

      <div className="flex gap-2">
        {!reinforce && (
          <button
            type="button"
            onClick={() => preview.mutate(request())}
            disabled={!ready || preview.isPending}
            className="flex-1 rounded-lg border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            {preview.isPending ? "Оцінюємо…" : "Оцінити шанси"}
          </button>
        )}
        <button
          type="button"
          data-tutorial="send-march"
          onClick={() => send.mutate(request(), { onSuccess: onSent })}
          disabled={!ready || send.isPending}
          className="flex-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          {send.isPending ? "Відправляємо…" : "Відправити"}
        </button>
      </div>
    </div>
  );
}
