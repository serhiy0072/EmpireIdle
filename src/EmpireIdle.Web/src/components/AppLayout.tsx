import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Outlet, useLocation } from "react-router-dom";
import { useSession } from "../hooks/useSession";
import { logout } from "../lib/auth";
import { seedAccount } from "../lib/dev";
import { appendChatEvent } from "../lib/queries/chat";
import { useMailUnread } from "../lib/queries/mail";
import { DEFAULT_LANGUAGE, useChangeLanguage, useDailyCheckIn, usePlayerSettings } from "../lib/queries/player";
import { useVillage } from "../lib/queries/village";
import { onGameEvent } from "../lib/realtime/connection";
import { isShieldActive, shieldUntilLabel } from "../lib/shield";
import { useWallet } from "../lib/queries/wallet";
import TutorialOverlay from "../tutorial/TutorialOverlay";
import { useTutorial } from "../tutorial/useTutorial";
import AttackAlertBanner from "./AttackAlertBanner";
import ErrorBanner from "./ErrorBanner";
import ErrorBoundary from "./ErrorBoundary";
import PowerBadge from "./PowerBadge";
import ResourceBar from "./ResourceBar";
import BottomNav from "./shell/BottomNav";

/** Назви мов їхніми ж мовами — так гравець упізнає свою, навіть не розуміючи поточної. */
const LANGUAGE_NAMES: Record<string, string> = { uk: "Українська", en: "English" };

export default function AppLayout() {
  const session = useSession();
  const queryClient = useQueryClient();
  const location = useLocation();
  const playerId = session?.playerId ?? "";
  const village = useVillage(playerId);
  const wallet = useWallet(playerId);
  const tutorial = useTutorial(playerId);
  const settings = usePlayerSettings(playerId);
  const mailUnread = useMailUnread(playerId);
  const changeLanguage = useChangeLanguage(playerId);
  const language = settings.data?.language ?? DEFAULT_LANGUAGE;
  const [menu, setMenu] = useState(false);

  useDailyCheckIn(playerId);

  // Чат дописується тут, а не на сторінці чату: інакше історія закритого чату
  // застаріла б, а перечитувати її за таймером нема потреби
  useEffect(
    () => onGameEvent("ChatMessage", (event) => appendChatEvent(queryClient, playerId, language, event)),
    [queryClient, playerId, language],
  );

  const seed = useMutation({
    mutationFn: () => seedAccount(playerId),
    // Сід наливає все одразу: точково інвалідувати нема сенсу
    onSuccess: () => void queryClient.invalidateQueries(),
  });

  return (
    // Екран — рівно висота вікна: HUD зверху, панель розділів знизу, між ними прокручується екран.
    // Так село займає весь залишок без підібраних на око відступів і сторінка під ним не їздить
    <div className="flex h-dvh flex-col bg-gradient-to-b from-[#cfdcf0] to-[#e8eef8]">
      <header className="relative z-40 shrink-0 border-b-2 border-[#5b85c4] bg-gradient-to-b from-[#24467f] to-[#16305e] text-white shadow-lg">
        <div className="mx-auto flex max-w-5xl flex-wrap items-center gap-2 px-3 py-2">
          <PowerBadge playerId={playerId} village={village.data} />
          <ResourceBar village={village.data} />

          <div className="ml-auto flex items-center gap-1.5">
            {isShieldActive(village.data?.shieldUntil) && (
              <span
                className="rounded-full bg-emerald-500/80 px-2.5 py-0.5 text-sm font-semibold ring-1 ring-emerald-200"
                title="Щит після падіння міста: вас не можуть атакувати. Ваш напад на гравця його знімає."
              >
                🛡 до {shieldUntilLabel(village.data?.shieldUntil)}
              </span>
            )}
            <span className="rounded-full bg-gradient-to-b from-violet-400 to-violet-700 px-3 py-0.5 text-sm font-semibold tabular-nums ring-1 ring-violet-200">
              💎 {wallet.data === undefined ? "…" : wallet.data.gemBalance.toLocaleString("uk-UA")}
            </span>

            <div className="relative">
              <button
                type="button"
                onClick={() => setMenu((open) => !open)}
                aria-expanded={menu}
                aria-label="Налаштування"
                className="rounded-full bg-black/30 px-2 py-0.5 text-lg ring-1 ring-white/15 hover:bg-black/40"
              >
                ⚙
              </button>
              {menu && (
                <div className="absolute right-0 top-full z-50 mt-2 w-48 space-y-2 rounded-xl bg-[#1d3b6f] p-3 shadow-2xl ring-2 ring-[#5b85c4]">
                  {(settings.data?.languages.length ?? 0) > 1 && (
                    <select
                      value={language}
                      onChange={(event) => changeLanguage.mutate(event.target.value)}
                      disabled={changeLanguage.isPending}
                      aria-label="Мова"
                      className="w-full rounded-lg bg-white px-2 py-1 text-sm text-slate-700"
                    >
                      {settings.data?.languages.map((code) => (
                        <option key={code} value={code}>
                          {LANGUAGE_NAMES[code] ?? code}
                        </option>
                      ))}
                    </select>
                  )}

                  {import.meta.env.DEV && (
                    <button
                      type="button"
                      onClick={() => seed.mutate()}
                      disabled={seed.isPending}
                      className="w-full rounded-lg bg-amber-500 px-3 py-1 text-sm font-semibold hover:bg-amber-400 disabled:opacity-50"
                    >
                      {seed.isPending ? "Наливаємо…" : "Сід"}
                    </button>
                  )}

                  <button type="button" onClick={logout} className="w-full rounded-lg bg-white/10 px-3 py-1 text-sm hover:bg-white/20">
                    Вийти
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="mx-auto max-w-5xl space-y-2 px-3 empty:hidden">
          <AttackAlertBanner />
          <ErrorBanner error={seed.error} />
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <main className="mx-auto h-full max-w-5xl px-4 py-4">
          {/* key скидає впалий екран при переході: навігація лишається живою */}
          <ErrorBoundary key={location.pathname}>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>

      <BottomNav mailUnread={mailUnread.data ?? 0} />

      {tutorial.step !== null && (
        <TutorialOverlay step={tutorial.step} onDismiss={tutorial.dismiss} onSkip={tutorial.skip} />
      )}
    </div>
  );
}
