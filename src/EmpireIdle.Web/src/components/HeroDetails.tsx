import { useState } from "react";
import { heroState, passivePercent, rankLabel, rankStyle, useCatalog } from "../lib/queries/catalog";
import type { HeroSummary } from "../lib/queries/heroes";
import HeroPortrait from "./heroes/HeroPortrait";

interface Props {
  hero: HeroSummary;
  /** Пул досвіду гравця (GDD §6.1): з нього качається будь-який герой. */
  experience: number;
  /** Універсальні осколки рідкості цього героя в інвентарі. */
  universalShards: number;
  busy: boolean;
  onLevelUp: () => void;
  onResetLevel: () => void;
  onAdvanceStar: () => void;
  onConvertShards: (count: number) => void;
  onEvolve: () => void;
  onAppointLeader: () => void;
  onHeal: () => void;
}

export default function HeroDetails({
  hero,
  experience,
  universalShards,
  busy,
  onLevelUp,
  onResetLevel,
  onAdvanceStar,
  onConvertShards,
  onEvolve,
  onAppointLeader,
  onHeal,
}: Props) {
  const catalog = useCatalog();
  const config = catalog.hero(hero.heroKey);
  const [confirmingReset, setConfirmingReset] = useState(false);

  const atLevelCap = hero.level >= hero.maxLevel;
  const stars = Math.floor(hero.starParts / catalog.partsPerStar);
  const starCost = hero.nextStarPartCost ?? null;
  const shardsMissing = starCost === null ? 0 : Math.max(0, starCost - hero.shards);
  const affordable = experience >= hero.experienceToNext;
  const atTierCap = hero.tier >= catalog.maxTier;
  const stationed = hero.stationedGarrisonId !== null && hero.stationedGarrisonId !== undefined;

  return (
    <aside className="rounded-xl border border-slate-200 bg-white p-4 space-y-4">
      <div>
        <div className="flex gap-3">
          <HeroPortrait heroKey={hero.heroKey} heroClass={config?.class} rank={config?.rank} tier={hero.tier} size={88} />

          <div className="min-w-0">
            <h2 className="text-lg font-medium text-slate-800">{catalog.heroName(hero.heroKey)}</h2>

            {config !== null && (
              <div className="mt-1 flex flex-wrap items-center gap-1 text-xs">
                <span className={`rounded px-2 py-0.5 ${rankStyle(config.rank)}`}>{rankLabel(config.rank)}</span>
                <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">{config.class}</span>
                <span className="rounded bg-slate-100 px-2 py-0.5 text-slate-600">швидкість {config.speed}</span>
              </div>
            )}
          </div>
        </div>

        <p className="mt-2 text-sm text-slate-500">
          Тір {hero.tier} з {catalog.maxTier} · рівень {hero.level} з {hero.maxLevel} · зірки {stars}/{catalog.maxStars}
          {hero.starParts % catalog.partsPerStar > 0 && ` (+${hero.starParts % catalog.partsPerStar}/${catalog.partsPerStar})`} ·
          осколків {hero.shards}
        </p>
        <p className="mt-1 text-sm text-slate-600">{heroState(hero.state)}</p>

        {config?.description !== null && config?.description !== undefined && (
          <p className="mt-2 text-sm text-slate-500">{config.description}</p>
        )}

        {/* Картка героя щільна: історію розгортають на вимогу, повністю вона — у кодексі */}
        {config?.lore !== null && config?.lore !== undefined && config.lore !== "" && (
          <details className="mt-2 text-sm">
            <summary className="cursor-pointer text-xs font-medium uppercase tracking-wide text-slate-500">Історія</summary>
            <p className="mt-1 border-l-2 border-amber-300 pl-3 italic leading-relaxed text-slate-600">{config.lore}</p>
          </details>
        )}
      </div>

      {config !== null && config.passives.length > 0 && (
        <section>
          <h3 className="text-xs font-medium uppercase tracking-wide text-slate-500">Вміння</h3>
          <ul className="mt-2 space-y-2">
            {config.passives.map((passive) => {
              const percent = passivePercent(passive, stars);

              return (
                <li key={passive.key} className="text-sm">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className={percent === null ? "text-slate-400" : "text-slate-800"}>{passive.displayName}</span>
                    <span className={percent === null ? "text-xs text-slate-400" : "text-xs text-emerald-700"}>
                      {percent === null ? `з ${passive.unlockStars} зірки` : `+${percent.toFixed(1)}%`}
                    </span>
                  </div>
                  <p className="text-xs text-slate-500">
                    {passive.stat} · {passive.target === "all" ? "усе військо" : passive.target}
                  </p>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <div className="space-y-2">
        <button
          type="button"
          onClick={onLevelUp}
          disabled={busy || atLevelCap || !affordable}
          className="w-full rounded-lg bg-emerald-600 px-3 py-2 text-sm font-medium text-white hover:bg-emerald-700 disabled:opacity-50"
        >
          {atLevelCap
            ? "Найвищий рівень"
            : `Підняти рівень · ${hero.experienceToNext.toLocaleString("uk-UA")} досвіду`}
        </button>
        {!atLevelCap && !affordable && (
          <p className="text-xs text-slate-500">
            Бракує {(hero.experienceToNext - experience).toLocaleString("uk-UA")} досвіду — його дають баночки досвіду.
          </p>
        )}

        {/* Зірки за осколки героя (GDD §6.1): кожна частинка — +5% бойової міці */}
        <button
          type="button"
          onClick={onAdvanceStar}
          disabled={busy || starCost === null || shardsMissing > 0}
          className="w-full rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-900 hover:bg-amber-100 disabled:opacity-50"
        >
          {starCost === null ? "Усі зірки заповнені" : `Заповнити частинку зірки · ${starCost} осколків`}
        </button>
        {shardsMissing > 0 && universalShards > 0 && (
          <button
            type="button"
            onClick={() => onConvertShards(Math.min(shardsMissing, universalShards))}
            disabled={busy}
            className="w-full rounded-lg border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Добрати {Math.min(shardsMissing, universalShards)} з універсальних осколків (є {universalShards})
          </button>
        )}

        {/* Скидання не скасувати — тож другий клік; досвід повертається в пул мінус 1% */}
        {hero.level > 1 &&
          (confirmingReset ? (
            <div className="flex items-center gap-2 text-sm">
              <span className="flex-1 text-slate-600">Скинути на 1 рівень? Досвід повернеться мінус 1%.</span>
              <button
                type="button"
                onClick={() => {
                  setConfirmingReset(false);
                  onResetLevel();
                }}
                disabled={busy}
                className="rounded-lg bg-rose-600 px-3 py-1 text-white hover:bg-rose-700 disabled:opacity-50"
              >
                Так
              </button>
              <button
                type="button"
                onClick={() => setConfirmingReset(false)}
                className="rounded-lg border border-slate-300 px-3 py-1 text-slate-700 hover:bg-slate-50"
              >
                Ні
              </button>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => setConfirmingReset(true)}
              disabled={busy}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
            >
              Скинути рівень
            </button>
          ))}

        <button
          type="button"
          onClick={onEvolve}
          disabled={busy || atTierCap}
          className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
        >
          {atTierCap ? "Максимальний тір" : "Еволюція тіру"}
        </button>

        {stationed && !hero.isLeader && (
          <button
            type="button"
            onClick={onAppointLeader}
            disabled={busy}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50 disabled:opacity-50"
          >
            Призначити лідером гарнізону
          </button>
        )}

        {hero.state === "Wounded" && (
          <button
            type="button"
            onClick={onHeal}
            disabled={busy}
            className="w-full rounded-lg border border-rose-300 bg-rose-50 px-3 py-2 text-sm text-rose-800 hover:bg-rose-100 disabled:opacity-50"
          >
            Вилікувати
          </button>
        )}
      </div>

      <p className="text-xs text-slate-400">Стеля рівня — нижча з двох: рівень ратуші й тір × 10.</p>
    </aside>
  );
}
