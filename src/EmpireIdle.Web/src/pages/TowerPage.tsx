import { useMemo, useState } from "react";
import BattleReplayPlayer from "../components/battle/BattleReplayPlayer";
import BattleSummary from "../components/battle/BattleSummary";
import SquadPicker from "../components/battle/SquadPicker";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GamePanel, GameScreen, GameTabs } from "../components/game/GameUi";
import { RewardLine, TowerFloorPanel, TowerLeaderboardPanel, TowerStrip } from "../components/tower/TowerPanels";
import { useSession } from "../hooks/useSession";
import type { TowerStormResult } from "../lib/apiTypes";
import { useHeroes } from "../lib/queries/heroes";
import { useStormTowerFloor, useTower, useTowerLeaderboard } from "../lib/queries/tower";
import { loadSquad, saveSquad } from "../lib/squad";

type Tab = "tower" | "rating";

/** Розмір загону — той самий, що RealtimeBattle.SquadSize на сервері; сервер однаково перевірить. */
const SQUAD_SIZE = 5;

/** Бафи, що діють, як «атака +25%, захист +10%»; нульовий баф не показується. */
function buffLabel(attack: number, defense: number): string {
  const percent = (multiplier: number) => Math.round((multiplier - 1) * 100);
  const parts: string[] = [];

  if (attack > 1) parts.push(`атака +${percent(attack)}%`);
  if (defense > 1) parts.push(`захист +${percent(defense)}%`);

  return parts.join(", ");
}

/**
 * «Тисячна вежа»: поверхи по черзі, кожен проходиться раз, нагорода одноразова, спроби безлімітні.
 * Тут — де гравець, хто чекає на наступному поверсі, що дадуть найближчі поверхи й рейтинг сервера.
 */
export default function TowerPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const [tab, setTab] = useState<Tab>("tower");

  const tower = useTower(playerId);
  const leaderboard = useTowerLeaderboard(playerId, tab === "rating");
  const heroes = useHeroes(playerId);
  const storm = useStormTowerFloor(playerId);

  // Загін вежі запам'ятовується між візитами: гравець штурмує поверх за поверхом тим самим складом
  const storageKey = `tower-squad:${playerId}`;
  const ready = useMemo(() => (heroes.data?.heroes ?? []).filter((hero) => hero.state !== "Wounded"), [heroes.data?.heroes]);
  const [chosen, setChosen] = useState<(string | null)[] | null>(null);
  const lanes = chosen ?? loadSquad(storageKey, SQUAD_SIZE, ready);
  const [result, setResult] = useState<TowerStormResult | null>(null);
  // Бос береться з поверху на момент штурму: після перемоги вежа вже показує наступний
  const [bossLane, setBossLane] = useState<number | null>(null);
  const [watching, setWatching] = useState(false);

  if (tower.isPending) return <p className="text-slate-500">Підіймаємося сходами…</p>;
  if (tower.isError) return <ErrorBanner error={tower.error} />;

  const view = tower.data;
  const buffed = view.attackBuff > 1 || view.defenseBuff > 1;
  const next = view.next;

  const changeSquad = (updated: (string | null)[]) => {
    setChosen(updated);
    saveSquad(storageKey, updated);
  };

  const attack = () => {
    if (next == null) return;

    const boss = next.enemies.find((enemy) => enemy.boss)?.lane ?? null;

    storm.mutate(
      { floor: next.number, lanes },
      {
        onSuccess: (outcome) => {
          setResult(outcome);
          setBossLane(boss);
          setWatching(true);
        },
      },
    );
  };

  return (
    <GameScreen
      title="Тисячна вежа"
      actions={
        <p className="text-sm text-sky-100">
          Пройдено {view.highestFloor} / {view.maxFloor}
          {view.rank != null && ` · місце ${view.rank}`}
        </p>
      }
    >
      <GameTabs<Tab>
        tabs={[
          { key: "tower", label: "Вежа" },
          { key: "rating", label: "Рейтинг" },
        ]}
        value={tab}
        onChange={setTab}
      />
      <GamePanel className="space-y-3">
        {tab === "rating" ? (
          leaderboard.isPending ? (
            <p className="text-sm text-sky-200">Рахуємо поверхи…</p>
          ) : leaderboard.isError ? (
            <ErrorBanner error={leaderboard.error} />
          ) : (
            <TowerLeaderboardPanel board={leaderboard.data} playerId={playerId} />
          )
        ) : (
          <>
            {!view.unlocked && (
              <p className="rounded-lg bg-amber-900/50 px-3 py-2 text-sm ring-1 ring-amber-300">
                Вежа відкривається з ратушею {view.requiresMainBuildingLevel} рівня.
              </p>
            )}

            {/* Бафи міста й звірів діють у вежі — гравець бачить їх до бою, а не після */}
            <p className="text-xs text-sky-200">
              {buffed
                ? `Діють бафи: ${buffLabel(view.attackBuff, view.defenseBuff)}`
                : "Бафів немає: бусти крамниці й звірі підсилюють загін у вежі"}
            </p>

            {result !== null && watching && (
              // Ключ — номер поверху й момент: новий штурм починає відтворення з нуля
              <BattleReplayPlayer key={`${result.floor}-${storm.submittedAt}`} replay={result.battle} bossLane={bossLane} onFinished={() => setWatching(false)} />
            )}

            {result !== null && !watching && (
              <div
                className={`space-y-2 rounded-xl p-3 text-center ring-1 ${result.victory ? "bg-emerald-900/50 ring-emerald-300" : "bg-rose-900/50 ring-rose-300"}`}
              >
                <p className="text-base font-semibold">
                  {result.victory ? `Поверх ${result.floor} пройдено!` : `Поверх ${result.floor} вистояв`}
                </p>
                {result.victory ? (
                  <p className="text-sm">
                    <RewardLine rewards={result.rewards} />
                  </p>
                ) : (
                  // Бій сталий для того самого складу: повтор без змін дасть той самий результат
                  <p className="text-sm text-rose-100">Змініть склад чи розстановку або прокачайте героїв — той самий загін програє так само.</p>
                )}
                <BattleSummary replay={result.battle} />
                <div className="flex justify-center">
                  <GameButton variant="secondary" onClick={() => setResult(null)}>
                    Далі
                  </GameButton>
                </div>
              </div>
            )}

            {next != null ? (
              <TowerFloorPanel floor={next} />
            ) : (
              <p className="text-center text-base font-semibold text-amber-300">Вежу підкорено — усі поверхи пройдено!</p>
            )}

            {next != null && view.unlocked && !watching && (
              <div className="space-y-2">
                <SquadPicker heroes={ready} lanes={lanes} onChange={changeSquad} />
                <ErrorBanner error={storm.error} />
                <div className="flex justify-end">
                  <GameButton variant="accent" onClick={attack} disabled={storm.isPending || lanes.every((id) => id === null)}>
                    {storm.isPending ? "Бій…" : `Штурм поверху ${next.number}`}
                  </GameButton>
                </div>
              </div>
            )}

            {view.upcoming.length > 0 && <TowerStrip floors={view.upcoming} />}
          </>
        )}
      </GamePanel>
    </GameScreen>
  );
}
