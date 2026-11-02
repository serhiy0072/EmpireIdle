import { useState } from "react";
import { Link, Navigate, useNavigate, useParams } from "react-router-dom";
import BeastListPopup from "../components/beasts/BeastListPopup";
import { BeastFeedPanel, BeastPassivePanel } from "../components/beasts/BeastPanels";
import { beastArtUrl } from "../components/beasts/beastArt";
import ErrorBanner from "../components/ErrorBanner";
import { GameScreen } from "../components/game/GameUi";
import HungerBanner from "../components/HungerBanner";
import { HOTKEY_PRIORITY, useHotkeys } from "../hooks/useHotkeys";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { FEED_ITEM, useActivateBeast, useBeastPen, useFeedBeast } from "../lib/queries/beasts";
import { useCatalog } from "../lib/queries/catalog";
import { useInventory } from "../lib/queries/inventory";
import { t } from "i18next";

/**
 * Екран звіра (референс 21.10.2026): звір на весь екран, стрілки між звірами загону, рівень і годування,
 * пасивка. Сюди ведуть натиск на звіра в загоні й «Список звірів».
 */
export default function BeastPage() {
  const { beastKey = "" } = useParams();
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const navigate = useNavigate();
  const catalog = useCatalog();
  const now = useNow();

  const pen = useBeastPen(playerId);
  const inventory = useInventory(playerId);
  const feed = useFeedBeast(playerId);
  const activate = useActivateBeast(playerId);
  const [list, setList] = useState(false);

  const beasts = pen.data?.beasts ?? [];
  const position = beasts.findIndex((beast) => beast.beastKey === beastKey);

  const go = (step: number) => {
    const next = beasts[(position + step + beasts.length) % beasts.length];
    if (next !== undefined) void navigate(`/beasts/${next.beastKey}`, { replace: true });
  };

  useHotkeys(
    { ArrowLeft: () => go(-1), ArrowRight: () => go(1) },
    { priority: HOTKEY_PRIORITY.neighbours, enabled: beasts.length > 1 },
  );

  if (pen.isPending) return <p className="text-sm text-slate-500">{t("village:beasts.loading")}</p>;
  if (pen.isError) return <ErrorBanner error={pen.error} />;

  const beast = beasts[position];

  // Такого звіра в загоні немає (відпустили чи хибне посилання) — назад до загону
  if (beast === undefined) return <Navigate to="/beasts" replace />;

  const feedLeft = inventory.data?.items.find((item) => item.itemKey === FEED_ITEM)?.count ?? 0;
  const busy = feed.isPending || activate.isPending;
  const arrow = "px-3 text-4xl text-sky-200 hover:text-white disabled:opacity-30";

  return (
    <div className="space-y-3">
      {pen.data.isHungry && <HungerBanner kind="beasts" />}
      <ErrorBanner error={feed.error ?? activate.error} />

      <GameScreen
        title={
          <span className="flex items-center gap-2">
            <Link to="/beasts" className="text-2xl leading-none text-sky-200 hover:text-white" aria-label={t("village:beasts.toEnclosure")}>
              ←
            </Link>
            {catalog.beastName(beast.beastKey)}
          </span>
        }
        actions={
          <button type="button" onClick={() => setList(true)} className="text-sm text-sky-200 hover:underline">
            📋 {t("village:beasts.listTitle")}
          </button>
        }
      >
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <button type="button" onClick={() => go(-1)} disabled={beasts.length < 2} className={arrow} aria-label={t("village:beasts.previous")}>
              ‹
            </button>
            <div className="relative flex h-56 flex-1 items-end justify-center overflow-hidden rounded-2xl bg-gradient-to-b from-sky-200 via-sky-100 to-emerald-300 sm:h-72">
              <div className="absolute bottom-6 h-6 w-48 rounded-[50%] bg-black/20 blur-sm" />
              <img src={beastArtUrl(beast.beastKey)} alt={catalog.beastName(beast.beastKey)} className="relative mb-3 h-48 sm:h-64" />
            </div>
            <button type="button" onClick={() => go(1)} disabled={beasts.length < 2} className={arrow} aria-label={t("village:beasts.next")}>
              ›
            </button>
          </div>

          <BeastFeedPanel
            beast={beast}
            feedLeft={feedLeft}
            busy={busy}
            onFeed={(count) => feed.mutate({ beastKey: beast.beastKey, count })}
          />
          <BeastPassivePanel beast={beast} now={now} busy={busy} onActivate={() => activate.mutate(beast.beastKey)} />
        </div>
      </GameScreen>

      {list && (
        <BeastListPopup
          beasts={beasts}
          name={catalog.beastName}
          onPick={(key) => {
            setList(false);
            void navigate(`/beasts/${key}`, { replace: true });
          }}
          onClose={() => setList(false)}
        />
      )}
    </div>
  );
}
