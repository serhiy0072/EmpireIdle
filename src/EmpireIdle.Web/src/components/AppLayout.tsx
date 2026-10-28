import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";
import { HOTKEY_PRIORITY, useHotkeys } from "../hooks/useHotkeys";
import { useAttention } from "../hooks/useAttention";
import { useSession } from "../hooks/useSession";
import { logout } from "../lib/auth";
import { seedAccount } from "../lib/dev";
import { appendChatEvent } from "../lib/queries/chat";
import { LANGUAGE_NAMES, setLanguage, useLanguage } from "../lib/i18n";
import { useChangeLanguage, useDailyCheckIn, usePlayerSettings } from "../lib/queries/player";
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
import ChatTicker from "./shell/ChatTicker";

export default function AppLayout() {
  const { t } = useTranslation();
  const session = useSession();
  const queryClient = useQueryClient();
  const location = useLocation();
  const navigate = useNavigate();
  const playerId = session?.playerId ?? "";
  const village = useVillage(playerId);
  const wallet = useWallet(playerId);
  const tutorial = useTutorial(playerId);
  const settings = usePlayerSettings(playerId);
  const attention = useAttention(playerId);
  const changeLanguage = useChangeLanguage(playerId);
  const language = useLanguage();
  const accountLanguage = settings.data?.language;
  // Меню прив'язане до екрана, де його відкрили: перехід закриває його без ефекту
  const [menuPath, setMenuPath] = useState<string | null>(null);
  const menu = menuPath === location.pathname;
  const closeMenu = () => setMenuPath(null);
  const menuRef = useRef<HTMLDivElement>(null);
  // Село й мапа — поле на весь екран між HUD і панеллю, без полів сторінки
  const fullBleed = location.pathname === "/" || location.pathname === "/map";

  useDailyCheckIn(playerId);

  // Мова акаунта важить більше за вибір пристрою: увійшли з іншого браузера — інтерфейс наздожене акаунт
  useEffect(() => {
    if (accountLanguage !== undefined) void setLanguage(accountLanguage);
  }, [accountLanguage]);

  // Esc — назад (рішення 21.10.2026): на попередній екран, а без історії (відкрили за посиланням) — у село.
  // Село — дім, далі назад нікуди. Вікна й вибране на екрані реєструють свій Esc вище й закриваються першими
  useHotkeys(
    { Escape: () => void (location.key !== "default" ? navigate(-1) : navigate("/")) },
    { priority: HOTKEY_PRIORITY.shell, enabled: location.pathname !== "/" },
  );
  useHotkeys({ Escape: closeMenu }, { modal: true, enabled: menu });

  // Клік повз меню закриває його, як будь-який спливний список
  useEffect(() => {
    if (!menu) return;

    const onPointerDown = (event: PointerEvent) => {
      if (!(menuRef.current?.contains(event.target as Node) ?? false)) setMenuPath(null);
    };

    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [menu]);

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
          {/* Додому, у село — замість кнопки «Село» на панелі (рішення 21.10.2026); на неї ж вказує навчання */}
          {location.pathname !== "/" && (
            <Link
              to="/"
              data-tutorial="nav:/"
              aria-label={t("shell.home")}
              title={t("shell.home")}
              className="flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-b from-sky-300 to-sky-600 text-lg font-bold text-white shadow ring-2 ring-sky-100 hover:brightness-110"
            >
              ←
            </Link>
          )}
          <PowerBadge playerId={playerId} village={village.data} />
          <ResourceBar village={village.data} />

          <div className="ml-auto flex items-center gap-1.5">
            {isShieldActive(village.data?.shieldUntil) && (
              <span
                className="rounded-full bg-emerald-500/80 px-2.5 py-0.5 text-sm font-semibold ring-1 ring-emerald-200"
                title={t("shell.shieldHint")}
              >
                {t("shell.shield", { until: shieldUntilLabel(village.data?.shieldUntil) })}
              </span>
            )}
            <span className="rounded-full bg-gradient-to-b from-violet-400 to-violet-700 px-3 py-0.5 text-sm font-semibold tabular-nums ring-1 ring-violet-200">
              💎 {wallet.data === undefined ? "…" : wallet.data.gemBalance.toLocaleString("uk-UA")}
            </span>

            <div ref={menuRef} className="relative">
              <button
                type="button"
                onClick={() => setMenuPath(menu ? null : location.pathname)}
                aria-expanded={menu}
                aria-label={t("shell.settings")}
                className="rounded-full bg-black/30 px-2 py-0.5 text-lg ring-1 ring-white/15 hover:bg-black/40"
              >
                ⚙
              </button>
              {menu && (
                <div className="absolute right-0 top-full z-50 mt-2 w-48 space-y-2 rounded-xl bg-[#1d3b6f] p-3 shadow-2xl ring-2 ring-[#5b85c4]">
                  {(settings.data?.languages.length ?? 0) > 1 && (
                    <select
                      value={accountLanguage ?? language}
                      onChange={(event) => changeLanguage.mutate(event.target.value)}
                      disabled={changeLanguage.isPending}
                      aria-label={t("shell.language")}
                      className="w-full rounded-lg bg-white px-2 py-1 text-sm text-slate-700"
                    >
                      {settings.data?.languages.map((code) => (
                        <option key={code} value={code}>
                          {LANGUAGE_NAMES[code as keyof typeof LANGUAGE_NAMES] ?? code}
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
                      {seed.isPending ? t("shell.seeding") : t("shell.seed")}
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() => {
                      // Кеш — дані цього гравця: наступний, хто ввійде в цій вкладці, їх не побачить
                      queryClient.clear();
                      void logout();
                    }}
                    className="w-full rounded-lg bg-white/10 px-3 py-1 text-sm hover:bg-white/20"
                  >
                    {t("shell.logout")}
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
        <main className={fullBleed ? "h-full" : "mx-auto h-full max-w-5xl px-4 py-4"}>
          {/* key скидає впалий екран при переході: навігація лишається живою */}
          <ErrorBoundary key={location.pathname}>
            <Outlet />
          </ErrorBoundary>
        </main>
      </div>

      <div className="relative">
        {/* Чат — напівпрозора смужка над панеллю; на самому чаті вона не потрібна */}
        {location.pathname !== "/chat" && (
          <div className="pointer-events-none absolute bottom-full left-2 z-30">
            <ChatTicker playerId={playerId} />
          </div>
        )}
        <BottomNav attention={attention} />
      </div>

      {tutorial.step !== null && (
        <TutorialOverlay step={tutorial.step} onDismiss={tutorial.dismiss} onSkip={tutorial.skip} />
      )}
    </div>
  );
}
