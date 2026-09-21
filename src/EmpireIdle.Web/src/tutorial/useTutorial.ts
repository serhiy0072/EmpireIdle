import { useEffect, useMemo } from "react";
import { useBattleReports } from "../lib/queries/battleReports";
import { useCatalog } from "../lib/queries/catalog";
import { useGarrison } from "../lib/queries/garrison";
import { useHeroes } from "../lib/queries/heroes";
import { useMarches } from "../lib/queries/marches";
import { useQuests } from "../lib/queries/quests";
import { useMarkStepSeen, useSkipTutorial, useTutorialProgress } from "../lib/queries/tutorial";
import { useVillage } from "../lib/queries/village";
import { useWallet } from "../lib/queries/wallet";
import { activeStep, INTRO_STEPS, type TutorialSnapshot, type TutorialStep } from "./steps";

export interface TutorialController {
  step: TutorialStep | null;
  /** Закрити поточний крок кнопкою. */
  dismiss: () => void;
  skip: () => void;
}

/**
 * Активний крок навчання. Усі запити — ті самі, що й в екранів: кеш
 * спільний, тож туторіал не дає зайвого навантаження, лише тримає їх теплими.
 */
export function useTutorial(playerId: string): TutorialController {
  const catalog = useCatalog();
  const progress = useTutorialProgress(playerId);
  const village = useVillage(playerId);
  const garrison = useGarrison(playerId);
  const heroes = useHeroes(playerId);
  const quests = useQuests(playerId);
  const marches = useMarches(playerId);
  const reports = useBattleReports(playerId);
  const wallet = useWallet(playerId);
  const markSeen = useMarkStepSeen(playerId);
  const skipTutorial = useSkipTutorial(playerId);

  const snapshot = useMemo<TutorialSnapshot | null>(() => {
    // Без села й прогресу вирішувати нема з чого: краще мовчати, ніж показати не той крок
    if (!catalog.loaded || village.data === undefined || progress.data === undefined) return null;

    return {
      catalog,
      village: village.data,
      garrison: garrison.data,
      heroes: heroes.data,
      quests: quests.data,
      marches: marches.data,
      reports: reports.data,
      wallet: wallet.data,
      seen: new Set(progress.data.seenSteps),
      skipped: progress.data.skippedAt !== null && progress.data.skippedAt !== undefined,
    };
  }, [catalog, village.data, progress.data, garrison.data, heroes.data, quests.data, marches.data, reports.data, wallet.data]);

  const step = snapshot === null ? null : activeStep(snapshot);

  // Досягнутий крок фіксуємо як побачений: інакше після втрат у бою "навчіть 5 воїнів" повернувся б
  const achieved = useMemo(
    () =>
      snapshot === null
        ? []
        : INTRO_STEPS.filter((s) => s.done !== undefined && s.available(snapshot) && s.done(snapshot) && !snapshot.seen.has(s.key)),
    [snapshot],
  );

  const { mutate: mark } = markSeen;

  useEffect(() => {
    achieved.forEach((s) => mark(s.key));
  }, [achieved, mark]);

  return {
    step,
    dismiss: () => {
      if (step !== null) mark(step.key);
    },
    skip: () => skipTutorial.mutate(),
  };
}
