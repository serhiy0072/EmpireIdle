import { useState } from "react";
import ErrorBanner from "../components/ErrorBanner";
import { GamePanel, GameScreen, GameTabs } from "../components/game/GameUi";
import { TowerFloorPanel, TowerLeaderboardPanel, TowerStrip } from "../components/tower/TowerPanels";
import { useSession } from "../hooks/useSession";
import { useTower, useTowerLeaderboard } from "../lib/queries/tower";

type Tab = "tower" | "rating";

/** Множник бафа як відсоток надбавки: 1.25 → «+25%». */
function buffLabel(multiplier: number): string {
  return `+${Math.round((multiplier - 1) * 100)}%`;
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

  if (tower.isPending) return <p className="text-slate-500">Підіймаємося сходами…</p>;
  if (tower.isError) return <ErrorBanner error={tower.error} />;

  const view = tower.data;
  const buffed = view.attackBuff > 1 || view.defenseBuff > 1;

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
                ? `Діють бафи: атака ${buffLabel(view.attackBuff)}, захист ${buffLabel(view.defenseBuff)}`
                : "Бафів немає: бусти крамниці й звірі підсилюють загін у вежі"}
            </p>

            {view.next != null ? (
              <TowerFloorPanel floor={view.next} />
            ) : (
              <p className="text-center text-base font-semibold text-amber-300">Вежу підкорено — усі поверхи пройдено!</p>
            )}

            {view.upcoming.length > 0 && <TowerStrip floors={view.upcoming} />}
          </>
        )}
      </GamePanel>
    </GameScreen>
  );
}
