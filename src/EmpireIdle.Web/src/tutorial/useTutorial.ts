import { useEffect, useMemo, useState } from "react";
import { useLocation } from "react-router-dom";
import { useBattleReports } from "../lib/queries/battleReports";
import { useBeastPen } from "../lib/queries/beasts";
import { useCatalog } from "../lib/queries/catalog";
import { useClanHelp, useMyClan } from "../lib/queries/clans";
import { useGarrison } from "../lib/queries/garrison";
import { useHeroes } from "../lib/queries/heroes";
import { useInventory } from "../lib/queries/inventory";
import { useMailUnread } from "../lib/queries/mail";
import { useIncomingAttacks, useMarches } from "../lib/queries/marches";
import { useQuests } from "../lib/queries/quests";
import { useMarkStepSeen, useSkipTutorial, useTutorialProgress } from "../lib/queries/tutorial";
import { useVillage } from "../lib/queries/village";
import { useWallet } from "../lib/queries/wallet";
import { activeStep, introSteps, isIntroStep, type TutorialSnapshot, type TutorialStep } from "./steps";

/** Скільки контекстна підказка чекає після закритої, якщо гравець лишився на тому самому екрані. */
const HINT_PAUSE_MS = 90_000;

/** Миша чи тачскрін — за сесію не змінюється, тож рахуємо раз. */
const POINTER_FINE = typeof window !== "undefined" && window.matchMedia("(pointer: fine)").matches;

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
  const inventory = useInventory(playerId);
  const incoming = useIncomingAttacks(playerId);
  const unreadMail = useMailUnread(playerId);
  const beastPen = useBeastPen(playerId);
  const clan = useMyClan(playerId);
  const clanHelp = useClanHelp(playerId, clan.data != null);
  const route = useLocation().pathname;
  // Закрита контекстна підказка: наступна — на іншому екрані або після паузи
  const [pause, setPause] = useState<{ route: string; until: number } | null>(null);
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
      inventory: inventory.data,
      incoming: incoming.data,
      unreadMail: unreadMail.data ?? 0,
      beastPen: beastPen.data,
      clan: clan.data,
      clanHelp: clanHelp.data,
      pointerFine: POINTER_FINE,
      route,
      hintsPaused: pause !== null && pause.route === route,
      seen: new Set(progress.data.seenSteps),
      skipped: progress.data.skippedAt !== null && progress.data.skippedAt !== undefined,
    };
  }, [
    catalog,
    village.data,
    progress.data,
    garrison.data,
    heroes.data,
    quests.data,
    marches.data,
    reports.data,
    wallet.data,
    inventory.data,
    incoming.data,
    unreadMail.data,
    beastPen.data,
    clan.data,
    clanHelp.data,
    route,
    pause,
  ]);

  const step = snapshot === null ? null : activeStep(snapshot);

  // Досягнутий крок фіксуємо як побачений: інакше після втрат у бою "навчіть 5 воїнів" повернувся б
  const achieved = useMemo(
    () =>
      snapshot === null
        ? []
        : introSteps().filter((s) => s.done !== undefined && s.available(snapshot) && s.done(snapshot) && !snapshot.seen.has(s.key)),
    [snapshot],
  );

  const { mutate: mark } = markSeen;

  useEffect(() => {
    achieved.forEach((s) => mark(s.key));
  }, [achieved, mark]);

  // Пауза минає сама: таймер знімає її, навіть якщо гравець так і не пішов з екрана
  useEffect(() => {
    if (pause === null) return;

    const timer = window.setTimeout(() => setPause(null), Math.max(pause.until - Date.now(), 0));
    return () => window.clearTimeout(timer);
  }, [pause]);

  return {
    step,
    dismiss: () => {
      if (step === null) return;

      mark(step.key);
      if (!isIntroStep(step)) setPause({ route, until: Date.now() + HINT_PAUSE_MS });
    },
    skip: () => skipTutorial.mutate(),
  };
}
