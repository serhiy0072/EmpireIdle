import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { onGameEvent } from "../lib/realtime/connection";

/** Остання тривога: що сталося, де, і ключ для переходу на мапу. */
interface Alert {
  key: string;
  text: string;
  x: number;
  y: number;
  destroyed: boolean;
}

function timeLabel(iso: string): string {
  return new Date(iso).toLocaleTimeString("uk-UA", { hour: "2-digit", minute: "2-digit" });
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
          ? `споруду клану${event.targetName == null ? "" : ` [${event.targetName}]`}`
          : (event.targetName ?? "село");

      setAlert({
        key: event.marchId,
        text: `${attacker} іде на ${target} (${event.targetX}, ${event.targetY}) — прибуде о ${timeLabel(event.arrivesAt)}.`,
        x: event.targetX,
        y: event.targetY,
        destroyed: false,
      });
    });

    const offDestroyed = onGameEvent("StructureDestroyed", (event) =>
      setAlert({
        key: event.structureId,
        text: `Споруду клану на (${event.x}, ${event.y}) зруйновано.`,
        x: event.x,
        y: event.y,
        destroyed: true,
      }),
    );

    return () => {
      offIncoming();
      offDestroyed();
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
        {!alert.destroyed && " Відправте підкріплення!"}
      </span>
      <div className="flex gap-2">
        <Link
          to={`/map?focus=${alert.x},${alert.y},${alert.key}`}
          onClick={() => setAlert(null)}
          className="rounded-lg border border-current px-2 py-0.5 hover:bg-white/50"
        >
          Показати на мапі
        </Link>
        <button type="button" onClick={() => setAlert(null)} className="px-1 hover:underline">
          Закрити
        </button>
      </div>
    </div>
  );
}
