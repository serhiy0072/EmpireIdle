import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { onGameEvent } from "../lib/realtime/connection";
import { formatClock } from "../lib/format";
import { t } from "i18next";

/** Остання тривога: що сталося, де, і ключ для переходу на мапу. */
interface Alert {
  key: string;
  text: string;
  x: number;
  y: number;
  destroyed: boolean;
  /** Розвідка: бою не буде, підкріплення кликати не треба. */
  scout: boolean;
}

function timeLabel(iso: string): string {
  return formatClock(iso);
}

/**
 * Тривога під шапкою: ворожий марш іде на своє село, соклановця чи споруду клану,
 * або споруду вже зруйновано. Клік веде на мапу — до цілі, де видно марш нападника.
 * Тримає лише останню тривогу: нова витісняє стару, закрити можна вручну.
 */
export default function AttackAlertBanner() {
  const [alert, setAlert] = useState<Alert | null>(null);

  useEffect(() => {
    const offIncoming = onGameEvent("AttackIncoming", (event) => {
      const attacker = `${event.attackerClanTag == null ? "" : `[${event.attackerClanTag}] `}${event.attackerName}`;
      const target =
        event.targetType === "ClanStructure"
          ? event.targetName == null
            ? t("village:alert.structure")
            : t("village:alert.namedStructure", { name: event.targetName })
          : (event.targetName ?? t("village:alert.village"));
      const where = { attacker, target, x: event.targetX, y: event.targetY, time: timeLabel(event.arrivesAt) };

      setAlert({
        key: event.marchId,
        text: event.intent === "Scout" ? t("village:alert.scouting", where) : t("village:alert.attacking", where),
        x: event.targetX,
        y: event.targetY,
        destroyed: false,
        scout: event.intent === "Scout",
      });
    });

    const offDestroyed = onGameEvent("StructureDestroyed", (event) =>
      setAlert({
        key: event.structureId,
        text: t("village:alert.destroyed", { x: event.x, y: event.y }),
        x: event.x,
        y: event.y,
        destroyed: true,
        scout: false,
      }),
    );

    // Напад зірвано — його тривога вже неправда; інші тривоги лишаємо
    const offCalledOff = onGameEvent("AttackCalledOff", (event) =>
      setAlert((current) => (current?.key === event.marchId ? null : current)),
    );

    return () => {
      offIncoming();
      offDestroyed();
      offCalledOff();
    };
  }, []);

  if (alert === null) return null;

  return (
    <div
      role="alert"
      className={`flex flex-wrap items-center justify-between gap-2 rounded-lg px-3 py-2 text-sm ${
        alert.destroyed ? "bg-rose-50 text-rose-900" : "bg-amber-50 text-amber-900"
      }`}
    >
      <span>
        {alert.text}
        {!alert.destroyed && !alert.scout && ` ${t("village:alert.reinforce")}`}
      </span>
      <div className="flex gap-2">
        <Link
          to={`/map?focus=${alert.x},${alert.y},${alert.key}`}
          onClick={() => setAlert(null)}
          className="rounded-lg border border-current px-2 py-0.5 hover:bg-white/50"
        >
          {t("village:alert.showOnMap")}
        </Link>
        <button type="button" onClick={() => setAlert(null)} className="px-1 hover:underline">
          {t("village:alert.close")}
        </button>
      </div>
    </div>
  );
}
