import { useMemo, useState } from "react";
import DungeonBattle from "../components/dungeons/DungeonBattle";
import ErrorBanner from "../components/ErrorBanner";
import HeroPortrait from "../components/heroes/HeroPortrait";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import type { DungeonRunView, DungeonView } from "../lib/apiTypes";
import { useCatalog } from "../lib/queries/catalog";
import { artifactRarityLabel, useDungeonRun, useDungeons, useStartDungeonRun } from "../lib/queries/dungeons";
import { useHeroes } from "../lib/queries/heroes";
import { rarityStyle } from "../lib/rarity";
import { formatRemaining } from "../lib/time";

/**
 * Данжі: вибір данжу й рівня, склад команди до чотирьох героїв і сам бій.
 * Незавершений забіг перехоплює екран — вийти з нього можна лише перемогою,
 * поразкою чи кнопкою «Вийти», бо енергію вже витрачено.
 */
export default function DungeonsPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();
  const catalog = useCatalog();

  const dungeons = useDungeons(playerId);
  const heroes = useHeroes(playerId);
  const start = useStartDungeonRun(playerId);

  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [level, setLevel] = useState(1);
  const [team, setTeam] = useState<string[]>([]);

  // Бій тримає сторінка, а не запит: після перемоги вітрина інвалідується,
  // і забіг зник би з-під гравця раніше, ніж той побачив результат
  const [battle, setBattle] = useState<DungeonRunView | null>(null);

  const [openedRunId, setOpenedRunId] = useState<string | null>(null);

  const activeRunId = dungeons.data?.activeRunId ?? null;
  const run = useDungeonRun(playerId, activeRunId !== null && battle === null);

  // Похідний стан під час рендера, а не в ефекті: забіг відкривається один раз
  // на свій ідентифікатор, і завершений бій не зникає, коли запит віддасть null
  if (run.data != null && run.data.runId !== openedRunId) {
    setOpenedRunId(run.data.runId);
    setBattle(run.data);
  }

  const idle = useMemo(
    () => (heroes.data?.heroes ?? []).filter((hero) => hero.state !== "Wounded"),
    [heroes.data?.heroes],
  );

  if (dungeons.isPending) {
    return <p className="text-slate-500">Запалюємо смолоскипи…</p>;
  }

  if (dungeons.isError) {
    return <ErrorBanner error={dungeons.error} />;
  }

  const overview = dungeons.data;

  // Незавершений забіг важливіший за вітрину: гравець уже в бою
  if (battle !== null) {
    const name = overview.dungeons.find((d) => d.key === battle.dungeonKey)?.displayName ?? battle.dungeonKey;

    return (
      <div className="space-y-4">
        <h1 className="text-xl font-medium text-slate-800">
          {name} · рівень {battle.level}
        </h1>
        <DungeonBattle playerId={playerId} run={battle} onFinished={() => setBattle(null)} />
      </div>
    );
  }

  const selected: DungeonView | null = overview.dungeons.find((d) => d.key === selectedKey) ?? null;
  const levelView = selected?.levels.find((l) => l.level === level) ?? null;

  const canStart =
    selected !== null &&
    selected.isUnlocked &&
    level <= selected.unlockedLevel &&
    team.length > 0 &&
    overview.energy >= overview.energyPerRun;

  const toggleHero = (heroId: string) =>
    setTeam((previous) =>
      previous.includes(heroId)
        ? previous.filter((id) => id !== heroId)
        : previous.length >= overview.teamSize
          ? previous
          : [...previous, heroId],
    );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Данжі</h1>
        <p className="text-sm text-slate-500">
          ⚡ {overview.energy} / {overview.maxEnergy} · забіг {overview.energyPerRun}
          {overview.energyFullAt != null && ` · повна за ${formatRemaining(overview.energyFullAt, now)}`}
        </p>
      </div>

      <ErrorBanner error={start.error} />

      <div className="grid gap-4 lg:grid-cols-[2fr_3fr]">
        <div className="space-y-2">
          {overview.dungeons.map((dungeon) => (
            <button
              key={dungeon.key}
              type="button"
              onClick={() => {
                setSelectedKey(dungeon.key);
                setLevel(Math.max(1, Math.min(dungeon.unlockedLevel, dungeon.clearedLevel + 1)));
              }}
              disabled={!dungeon.isUnlocked}
              className={`w-full rounded-xl border p-3 text-left transition ${
                selectedKey === dungeon.key ? "border-emerald-500 bg-emerald-50" : "border-slate-200 bg-white hover:border-slate-300"
              } ${dungeon.isUnlocked ? "" : "opacity-50"}`}
            >
              <div className="flex items-baseline justify-between gap-2">
                <span className="font-medium text-slate-800">{dungeon.displayName}</span>
                <span className="text-xs text-slate-500">
                  {dungeon.isUnlocked ? `пройдено ${dungeon.clearedLevel}/3` : `🔒 ратуша ${dungeon.requiresMainBuildingLevel}`}
                </span>
              </div>
              <p className="mt-1 text-xs text-slate-500">{dungeon.description}</p>
            </button>
          ))}
        </div>

        {selected === null ? (
          <p className="text-sm text-slate-500">Оберіть данж, щоб зібрати команду.</p>
        ) : (
          <div className="space-y-4 rounded-xl border border-slate-200 bg-white p-4">
            <section className="space-y-2">
              <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Рівень</h2>
              <div className="flex gap-2">
                {selected.levels.map((option) => {
                  const locked = option.level > selected.unlockedLevel;

                  return (
                    <button
                      key={option.level}
                      type="button"
                      onClick={() => setLevel(option.level)}
                      disabled={locked}
                      className={`flex-1 rounded-lg border p-2 text-center text-sm ${
                        level === option.level ? "border-emerald-500 bg-emerald-50" : "border-slate-200"
                      } ${locked ? "opacity-50" : "hover:border-slate-300"}`}
                    >
                      <div className="font-medium text-slate-800">Рівень {option.level}</div>
                      <div className={`mt-1 inline-block rounded px-2 py-0.5 text-xs ${rarityStyle(option.artifactRarity)}`}>
                        {artifactRarityLabel(option.artifactRarity)} артефакт
                      </div>
                      <div className="mt-1 text-xs text-slate-500">
                        {option.waves} хвилі · вороги ×{option.powerMultiplier}
                      </div>
                      {locked && <div className="text-xs text-slate-400">пройдіть рівень {option.level - 1}</div>}
                    </button>
                  );
                })}
              </div>
              {levelView !== null && (
                <p className="text-xs text-slate-500">
                  Нагорода: {levelView.reward.map((line) => `${catalog.resourceName(line.resource)} ${line.amount.toLocaleString("uk-UA")}`).join(" · ")}
                </p>
              )}
            </section>

            <section className="space-y-2">
              <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">
                Команда · {team.length}/{overview.teamSize}
              </h2>
              {idle.length === 0 ? (
                <p className="text-sm text-slate-500">Немає доступних героїв — усі поранені.</p>
              ) : (
                <div className="grid gap-2 sm:grid-cols-2">
                  {idle.map((hero) => {
                    const config = catalog.hero(hero.heroKey);
                    const picked = team.includes(hero.id);
                    const order = team.indexOf(hero.id) + 1;

                    return (
                      <button
                        key={hero.id}
                        type="button"
                        onClick={() => toggleHero(hero.id)}
                        className={`flex items-center gap-2 rounded-xl border p-2 text-left ${
                          picked ? "border-emerald-500 bg-emerald-50" : "border-slate-200 hover:border-slate-300"
                        }`}
                      >
                        <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={40} />
                        <div className="min-w-0 flex-1">
                          <div className="truncate text-sm font-medium text-slate-800">{catalog.heroName(hero.heroKey)}</div>
                          <div className="text-xs text-slate-500">
                            рів. {hero.level} · {config?.class ?? "—"}
                          </div>
                        </div>
                        {picked && <span className="text-xs font-medium text-emerald-700">#{order}</span>}
                      </button>
                    );
                  })}
                </div>
              )}
            </section>

            <button
              type="button"
              onClick={() => start.mutate({ dungeonKey: selected.key, level, heroIds: team }, { onSuccess: (started) => {
                  setOpenedRunId(started.runId);
                  setBattle(started);
                } })}
              disabled={!canStart || start.isPending}
              className="w-full rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              {overview.energy < overview.energyPerRun
                ? "Не вистачає енергії"
                : `Почати забіг · ${overview.energyPerRun} ⚡`}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
