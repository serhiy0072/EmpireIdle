import HeroPortrait from "../heroes/HeroPortrait";
import type { CombatantView } from "../../lib/apiTypes";
import { useCatalog } from "../../lib/queries/catalog";
import { statusLabel } from "../../lib/queries/dungeons";
import { t } from "i18next";

interface Props {
  combatant: CombatantView;
  /** Чий зараз хід — обведення показує, кого чекає бій. */
  active: boolean;
  /** Ціль можна обрати: підсвічуємо й даємо клік. */
  targetable: boolean;
  selected: boolean;
  /** Шкода за останній хід; показується сплеском над бійцем. */
  splash: { damage: number; healed: number; critical: boolean; blocked: boolean } | null;
  onSelect?: () => void;
}

function Bar({ value, max, className }: { value: number; max: number; className: string }) {
  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-white/15">
      <div className={`h-full ${className}`} style={{ width: `${Math.max(0, Math.min(100, (value / max) * 100))}%` }} />
    </div>
  );
}

/** Боєць у бою: портрет, здоров'я, перезарядка активного вміння й накладені стани. */
export default function CombatantCard({ combatant, active, targetable, selected, splash, onSelect }: Props) {
  const catalog = useCatalog();
  const hero = combatant.heroId == null ? null : catalog.hero(combatant.key);
  const dead = combatant.health <= 0;
  // Шкала перезарядки: повна — вміння готове (GDD §6.1, раз на кілька ходів героя)
  const skill = combatant.skills.find((s) => s.kind === "Active") ?? null;

  return (
    <button
      type="button"
      onClick={onSelect}
      disabled={!targetable}
      className={`relative w-full rounded-xl border p-2 text-left transition ${
        selected
          ? "border-rose-500 bg-rose-500/15"
          : active
            ? "border-amber-500 bg-amber-500/15"
            : "border-[#335c9a] bg-[#1d3b6f]"
      } ${dead ? "opacity-40 grayscale" : ""} ${targetable ? "cursor-pointer hover:border-rose-400" : "cursor-default"}`}
    >
      {splash !== null && (
        <span
          className={`pointer-events-none absolute -top-2 right-2 rounded-full px-2 py-0.5 text-xs font-semibold ${
            splash.healed > 0 ? "bg-emerald-500/25 text-emerald-200" : "bg-rose-500/25 text-rose-200"
          }`}
        >
          {splash.healed > 0 ? `+${Math.round(splash.healed)}` : `−${Math.round(splash.damage)}`}
          {splash.critical && "!"}
          {splash.blocked && " 🛡"}
        </span>
      )}

      <div className="flex items-center gap-2">
        {hero !== null ? (
          <HeroPortrait heroKey={combatant.key} heroClass={hero.class} rank={hero.rank} tier={1} size={36} />
        ) : (
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-800 text-lg">👹</span>
        )}

        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-1">
            <span className="truncate text-xs font-medium text-white">{combatant.displayName}</span>
            <span className="shrink-0 text-[10px] text-sky-100/70">{combatant.line === "Front" ? t("battle:line.front") : t("battle:line.back")}</span>
          </div>

          <div className="mt-1 space-y-1">
            <Bar value={combatant.health} max={combatant.maxHealth} className={dead ? "bg-slate-400" : "bg-emerald-500"} />
            {skill !== null && (
              <Bar
                value={skill.cooldown - skill.cooldownLeft}
                max={Math.max(1, skill.cooldown)}
                className={skill.ready ? "bg-amber-500" : "bg-sky-500"}
              />
            )}
          </div>

          <div className="mt-0.5 flex flex-wrap items-center gap-1 text-[10px] text-sky-100/70">
            <span>
              {Math.round(combatant.health)}/{Math.round(combatant.maxHealth)}
            </span>
            {combatant.shield > 0 && <span className="text-sky-200">{t("battle:combatant.shield", { value: Math.round(combatant.shield) })}</span>}
            {combatant.statuses.map((status) => (
              <span key={status.kind} className="rounded bg-white/10 px-1 text-slate-200">
                {statusLabel(status.kind)} {status.turnsLeft}
              </span>
            ))}
          </div>
        </div>
      </div>
    </button>
  );
}
