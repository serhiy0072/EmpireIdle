import { useEffect, useEffectEvent, useMemo, useRef, useState } from "react";
import BattleReplayPlayer from "../components/battle/BattleReplayPlayer";
import BattleSummary from "../components/battle/BattleSummary";
import SquadPicker from "../components/battle/SquadPicker";
import ErrorBanner from "../components/ErrorBanner";
import { GameButton, GamePanel, GameScreen, GameTabs } from "../components/game/GameUi";
import { RewardLine, TowerFloorPanel, TowerLeaderboardPanel, TowerStrip } from "../components/tower/TowerPanels";
import { useSession } from "../hooks/useSession";
import type { BattleReplay, ManualCast, TowerFloorView, TowerStormResult } from "../lib/apiTypes";
import { loadAutoSkills } from "../lib/battleSettings";
import { useHeroes } from "../lib/queries/heroes";
import { usePreviewTowerFloor, useStormTowerFloor, useTower, useTowerLeaderboard } from "../lib/queries/tower";
import { loadSquad, saveSquad } from "../lib/squad";
import { t } from "i18next";

type Tab = "tower" | "rating";

/** Бій, що зараз показується. */
interface Fight {
  /** Новий бій — нове відтворення з нуля, навіть на тому самому поверсі. */
  id: number;
  floor: number;
  replay: BattleReplay;
  /** Бос береться з поверху на момент штурму: після перемоги вежа вже показує наступний. */
  bossLane: number | null;
  /** Ручний бій: його ще зарахує штурм із наказами гравця. Авто вже зараховано — тут false. */
  manual: boolean;
}

/** Розмір загону — той самий, що RealtimeBattle.SquadSize на сервері; сервер однаково перевірить. */
const SQUAD_SIZE = 5;

/** Бафи, що діють, як «атака +25%, захист +10%»; нульовий баф не показується. */
function buffLabel(attack: number, defense: number): string {
  const percent = (multiplier: number) => Math.round((multiplier - 1) * 100);
  const parts: string[] = [];

  if (attack > 1) parts.push(t("battle:towerPage.attackBuff", { percent: percent(attack) }));
  if (defense > 1) parts.push(t("battle:towerPage.defenseBuff", { percent: percent(defense) }));

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
  const preview = usePreviewTowerFloor(playerId);

  // Загін вежі запам'ятовується між візитами: гравець штурмує поверх за поверхом тим самим складом
  const storageKey = `tower-squad:${playerId}`;
  const ready = useMemo(() => (heroes.data?.heroes ?? []).filter((hero) => hero.state !== "Wounded"), [heroes.data?.heroes]);
  const [chosen, setChosen] = useState<(string | null)[] | null>(null);
  const lanes = chosen ?? loadSquad(storageKey, SQUAD_SIZE, ready);
  const [fight, setFight] = useState<Fight | null>(null);
  const [result, setResult] = useState<TowerStormResult | null>(null);
  // Накази ручного бою від його початку — їх перераховує кожен перегляд і зараховує штурм
  const castsRef = useRef<ManualCast[]>([]);
  // Автоштурм: після кожної перемоги — наступний поверх, до першої поразки
  const [auto, setAuto] = useState(false);
  const next = tower.data?.next ?? null;
  // Вежу підкорено — автоштурму далі нікуди йти
  const autoRunning = auto && next != null;
  const busy = storm.isPending || preview.isPending;

  /** Штурм поверху. Вимкнене «Авто» у бою — ручний бій: спершу перегляд, зарахування — після нього. */
  const stormFloor = (floor: TowerFloorView, manualAllowed: boolean) => {
    const bossLane = floor.enemies.find((enemy) => enemy.boss)?.lane ?? null;

    if (manualAllowed && !loadAutoSkills()) {
      castsRef.current = [];
      preview.mutate(
        { floor: floor.number, lanes, casts: [] },
        {
          onSuccess: (replay) => {
            setResult(null);
            setFight({ id: Date.now(), floor: floor.number, replay, bossLane, manual: true });
          },
        },
      );
      return;
    }

    storm.mutate(
      { floor: floor.number, lanes },
      {
        onSuccess: (outcome) => {
          setResult(outcome);
          setFight({ id: Date.now(), floor: floor.number, replay: outcome.battle, bossLane, manual: false });
          if (!outcome.victory) setAuto(false);
        },
        onError: () => setAuto(false),
      },
    );
  };

  /** Наказ ручного бою: бій перераховується з усіма наказами; не дійшов — наказ знімається. */
  const castInFight = async (floor: number, lane: number, tick: number): Promise<BattleReplay> => {
    const before = castsRef.current;
    castsRef.current = [...before, { tick, lane }];

    try {
      return await preview.mutateAsync({ floor, lanes, casts: castsRef.current });
    } catch (error) {
      castsRef.current = before;
      throw error;
    }
  };

  /** Показ скінчився: ручний бій зараховує штурм із тими самими наказами — сервер перерахує той самий бій. */
  const finishFight = (finished: Fight) => {
    setFight(null);
    if (!finished.manual) return;

    storm.mutate({ floor: finished.floor, lanes, casts: castsRef.current }, { onSuccess: setResult });
  };

  const continueAuto = useEffectEvent((floor: TowerFloorView) => stormFloor(floor, false));

  // Наступний штурм — коли відтворення скінчилось і вежа вже показує наступний поверх
  useEffect(() => {
    if (!autoRunning || fight !== null || busy || result === null || !result.victory) return;
    if (next.number !== result.floor + 1) return;

    continueAuto(next);
  }, [autoRunning, fight, busy, result, next]);

  if (tower.isPending) return <p className="text-slate-500">{t("battle:towerPage.loading")}</p>;
  if (tower.isError) return <ErrorBanner error={tower.error} />;

  const view = tower.data;
  const buffed = view.attackBuff > 1 || view.defenseBuff > 1;
  const canStorm = !busy && lanes.some((id) => id !== null);

  const changeSquad = (updated: (string | null)[]) => {
    setChosen(updated);
    saveSquad(storageKey, updated);
  };

  const startAuto = () => {
    if (next == null) return;

    setAuto(true);
    stormFloor(next, false);
  };

  return (
    <GameScreen
      title={t("battle:towerPage.title")}
      actions={
        <p className="text-sm text-sky-100">
          {t("battle:towerPage.progress", { floor: view.highestFloor, max: view.maxFloor })}
          {view.rank != null && ` · ${t("battle:towerPage.rank", { rank: view.rank })}`}
        </p>
      }
    >
      <GameTabs<Tab>
        tabs={[
          { key: "tower", label: t("battle:towerPage.tabTower") },
          { key: "rating", label: t("battle:towerPage.tabRating") },
        ]}
        value={tab}
        onChange={setTab}
      />
      <GamePanel className="space-y-3">
        {tab === "rating" ? (
          leaderboard.isPending ? (
            <p className="text-sm text-sky-200">{t("battle:towerPage.ratingLoading")}</p>
          ) : leaderboard.isError ? (
            <ErrorBanner error={leaderboard.error} />
          ) : (
            <TowerLeaderboardPanel board={leaderboard.data} playerId={playerId} />
          )
        ) : (
          <>
            {!view.unlocked && (
              <p className="rounded-lg bg-amber-900/50 px-3 py-2 text-sm ring-1 ring-amber-300">
                {t("battle:towerPage.locked", { level: view.requiresMainBuildingLevel })}
              </p>
            )}

            {/* Бафи міста й звірів діють у вежі — гравець бачить їх до бою, а не після */}
            <p className="text-xs text-sky-200">
              {buffed
                ? t("battle:towerPage.buffs", { buffs: buffLabel(view.attackBuff, view.defenseBuff) })
                : t("battle:towerPage.noBuffs")}
            </p>

            {autoRunning && (
              <div className="flex items-center justify-between rounded-lg bg-amber-900/40 px-3 py-1.5 text-sm ring-1 ring-amber-300">
                <span>{t("battle:towerPage.autoRunning")}</span>
                <GameButton variant="secondary" onClick={() => setAuto(false)}>
                  {t("battle:towerPage.stop")}
                </GameButton>
              </div>
            )}

            {fight !== null && (
              // Ключ — номер бою: новий штурм починає відтворення з нуля
              <BattleReplayPlayer
                key={fight.id}
                replay={fight.replay}
                bossLane={fight.bossLane}
                onCast={fight.manual ? (lane, tick) => castInFight(fight.floor, lane, tick) : null}
                onFinished={() => finishFight(fight)}
              />
            )}

            {fight === null && storm.isPending && <p className="text-center text-sm text-sky-200">{t("battle:towerPage.counting")}</p>}

            {result !== null && fight === null && (
              <div
                className={`space-y-2 rounded-xl p-3 text-center ring-1 ${result.victory ? "bg-emerald-900/50 ring-emerald-300" : "bg-rose-900/50 ring-rose-300"}`}
              >
                <p className="text-base font-semibold">
                  {result.victory ? t("battle:towerPage.won", { floor: result.floor }) : t("battle:towerPage.lost", { floor: result.floor })}
                </p>
                {result.victory ? (
                  <p className="text-sm">
                    <RewardLine rewards={result.rewards} />
                  </p>
                ) : (
                  // Бій сталий для того самого складу: повтор без змін дасть той самий результат
                  <p className="text-sm text-rose-100">{t("battle:towerPage.lostHint")}</p>
                )}
                <BattleSummary replay={result.battle} />
                <div className="flex justify-center">
                  <GameButton variant="secondary" onClick={() => setResult(null)}>
                    {t("battle:towerPage.next")}
                  </GameButton>
                </div>
              </div>
            )}

            {next != null ? (
              <TowerFloorPanel floor={next} />
            ) : (
              <p className="text-center text-base font-semibold text-amber-300">{t("battle:towerPage.conquered")}</p>
            )}

            {next != null && view.unlocked && fight === null && (
              <div className="space-y-2">
                <SquadPicker heroes={ready} lanes={lanes} onChange={changeSquad} />
                <ErrorBanner error={storm.error ?? preview.error} />
                <div className="flex justify-end gap-2">
                  <GameButton variant="secondary" onClick={startAuto} disabled={!canStorm || autoRunning}>
                    {t("battle:towerPage.auto")}
                  </GameButton>
                  <GameButton variant="accent" onClick={() => stormFloor(next, true)} disabled={!canStorm || autoRunning}>
                    {storm.isPending ? t("battle:towerPage.fighting") : t("battle:towerPage.storm", { floor: next.number })}
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
