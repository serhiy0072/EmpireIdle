import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useSession } from "../hooks/useSession";
import { logout } from "../lib/auth";
import { seedAccount } from "../lib/dev";
import { appendChatEvent } from "../lib/queries/chat";
import { useMailUnread } from "../lib/queries/mail";
import { DEFAULT_LANGUAGE, useChangeLanguage, usePlayerSettings } from "../lib/queries/player";
import { useVillage } from "../lib/queries/village";
import { onGameEvent } from "../lib/realtime/connection";
import { isShieldActive, shieldUntilLabel } from "../lib/shield";
import { useWallet } from "../lib/queries/wallet";
import TutorialOverlay from "../tutorial/TutorialOverlay";
import { useTutorial } from "../tutorial/useTutorial";
import ErrorBanner from "./ErrorBanner";
import ErrorBoundary from "./ErrorBoundary";
import PowerBadge from "./PowerBadge";
import ResourceBar from "./ResourceBar";

const NAV_ITEMS = [
  { to: "/", label: "Село" },
  { to: "/army", label: "Військо" },
  { to: "/heroes", label: "Герої" },
  { to: "/dungeons", label: "Данжі" },
  { to: "/inventory", label: "Інвентар" },
  { to: "/forge", label: "Кузня" },
  { to: "/banners", label: "Банери" },
  { to: "/shop", label: "Крамниця" },
  { to: "/market", label: "Ринок" },
  { to: "/clan", label: "Клан" },
  { to: "/map", label: "Мапа" },
  { to: "/quests", label: "Квести" },
  { to: "/rating", label: "Рейтинг" },
  { to: "/chat", label: "Чат" },
  { to: "/mail", label: "Скринька" },
] as const;

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
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto max-w-5xl space-y-2 px-4 py-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <ResourceBar village={village.data} />

            <div className="flex items-center gap-2">
              {isShieldActive(village.data?.shieldUntil) && (
                <span
                  className="rounded-full bg-emerald-100 px-3 py-1 text-sm text-emerald-800"
                  title="Щит після падіння міста: вас не можуть атакувати. Ваш напад на гравця його знімає."
                >
                  🛡 до {shieldUntilLabel(village.data?.shieldUntil)}
                </span>
              )}
              <PowerBadge playerId={playerId} village={village.data} />
              <span className="rounded-full bg-violet-100 px-3 py-1 text-sm text-violet-800">
                💎 <span className="font-medium">{(wallet.data?.gemBalance ?? 0).toLocaleString("uk-UA")}</span>
              </span>
              {(wallet.data?.sealBalance ?? 0) > 0 && (
                <span className="rounded-full bg-sky-100 px-3 py-1 text-sm text-sky-800" title="Печатки призову">
                  🔮 <span className="font-medium">{(wallet.data?.sealBalance ?? 0).toLocaleString("uk-UA")}</span>
                </span>
              )}

              {import.meta.env.DEV && (
                <button
                  type="button"
                  onClick={() => seed.mutate()}
                  disabled={seed.isPending}
                  className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-1 text-sm text-amber-800 hover:bg-amber-100 disabled:opacity-50"
                >
                  {seed.isPending ? "Наливаємо…" : "Сід"}
                </button>
              )}

              {(settings.data?.languages.length ?? 0) > 1 && (
                <select
                  value={language}
                  onChange={(event) => changeLanguage.mutate(event.target.value)}
                  disabled={changeLanguage.isPending}
                  aria-label="Мова"
                  className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700"
                >
                  {settings.data?.languages.map((code) => (
                    <option key={code} value={code}>
                      {LANGUAGE_NAMES[code] ?? code}
                    </option>
                  ))}
                </select>
              )}

              <button
                type="button"
                onClick={logout}
                className="rounded-lg border border-slate-300 px-3 py-1 text-sm text-slate-700 hover:bg-slate-100"
              >
                Вийти
              </button>
            </div>
          </div>

          <nav className="flex flex-wrap gap-1">
            {NAV_ITEMS.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                data-tutorial={`nav:${item.to}`}
                end
                className={({ isActive }) =>
                  `rounded-lg px-3 py-1 text-sm ${isActive ? "bg-slate-800 text-white" : "text-slate-600 hover:bg-slate-100"}`
                }
              >
                {item.label}
                {item.to === "/mail" && (mailUnread.data ?? 0) > 0 && (
                  <span className="ml-1 rounded-full bg-emerald-600 px-1.5 text-xs font-medium text-white">
                    {mailUnread.data}
                  </span>
                )}
              </NavLink>
            ))}
          </nav>

          <ErrorBanner error={seed.error} />
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-6">
        {/* key скидає впалий екран при переході: навігація лишається живою */}
        <ErrorBoundary key={location.pathname}>
          <Outlet />
        </ErrorBoundary>
      </main>

      {tutorial.step !== null && (
        <TutorialOverlay step={tutorial.step} onDismiss={tutorial.dismiss} onSkip={tutorial.skip} />
      )}
    </div>
  );
}
