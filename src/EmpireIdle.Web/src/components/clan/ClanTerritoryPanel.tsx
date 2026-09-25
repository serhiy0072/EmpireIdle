import { Link } from "react-router-dom";
import { useNow } from "../../hooks/useNow";
import type { ClanTerritoryResponse } from "../../lib/apiTypes";
import { formatRemaining } from "../../lib/time";

interface Props {
  territory: ClanTerritoryResponse;
  myPlayerId: string;
}

const number = (value: number) => value.toLocaleString("uk-UA");

/**
 * Територія клану (GDD §7.2): очки вкладу, слоти, споруди, квести клану й топ внеску.
 * Закладають і обслуговують споруди на мапі — тут огляд і шлях туди.
 */
export default function ClanTerritoryPanel({ territory, myPlayerId }: Props) {
  const now = useNow();

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex items-baseline justify-between gap-2">
          <h3 className="font-medium text-slate-800">Очки вкладу</h3>
          <span className="text-lg font-medium text-emerald-700">{number(territory.points)}</span>
        </div>
        <p className="text-sm text-slate-600">
          Набігають із боїв з монстрами (частка здобичі — вашої нагороди це не зменшує) і за квести клану.
        </p>

        {territory.enabled ? (
          <>
            <p className="text-sm text-slate-600">
              Споруда коштує {number(territory.structureCost)} очок, будується{" "}
              {Math.round(territory.buildMinutes / 60)} год (марші підкріплення прискорюють) і дає бонус до атаки й
              захисту всім учасникам у радіусі {territory.radius} клітин.
            </p>
            <p className="text-sm text-slate-700">
              Слоти: {territory.structures.length} / {territory.slotsOpen} відкрито · максимум {territory.slotsMax}
            </p>
            <ul className="space-y-1 text-sm">
              {territory.slotUnlocks.map((unlock, index) => {
                const quest = territory.quests.find((q) => q.key === unlock.questKey);
                const label =
                  unlock.minMembers != null
                    ? `${unlock.minMembers} учасників`
                    : `квест «${quest?.displayName ?? unlock.questKey}»`;

                return (
                  <li key={index} className={unlock.unlocked ? "text-emerald-700" : "text-slate-500"}>
                    {unlock.unlocked ? "✓" : "○"} Слот за {label}
                  </li>
                );
              })}
            </ul>
            {!territory.canBuild && (
              <p className="text-xs text-slate-500">Закладати й зносити споруди може роль із правом «споруди».</p>
            )}
          </>
        ) : (
          <p className="text-sm text-slate-500">У цьому світі споруд клану немає — очки чекають на них.</p>
        )}
      </section>

      {territory.enabled && (
        <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
          <div className="flex items-baseline justify-between gap-2">
            <h3 className="font-medium text-slate-800">Споруди</h3>
            <Link to="/map" className="text-sm text-emerald-700 hover:underline">
              На мапу
            </Link>
          </div>
          {territory.structures.length === 0 ? (
            <p className="text-sm text-slate-500">
              Жодної. {territory.canBuild ? "Оберіть вільну придатну клітину на мапі й закладіть першу." : ""}
            </p>
          ) : (
            <ul className="space-y-2">
              {territory.structures.map((structure) => {
                const building = Date.parse(structure.readyAt) > now;

                return (
                  <li key={structure.id} className="flex items-center justify-between gap-2 rounded-lg bg-slate-50 px-3 py-2 text-sm">
                    <span className="text-slate-800">
                      ({structure.x}, {structure.y})
                    </span>
                    <span className="text-slate-600">
                      гарнізон {structure.garrisonUnits}/{territory.garrisonCapacity}
                      {structure.myUnits > 0 && ` · ваших ${structure.myUnits}`}
                    </span>
                    <span className={building ? "font-mono text-amber-700" : "text-emerald-700"}>
                      {building ? formatRemaining(structure.readyAt, now) : "діє"}
                    </span>
                  </li>
                );
              })}
            </ul>
          )}
        </section>
      )}

      <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <h3 className="font-medium text-slate-800">Квести клану</h3>
        {territory.quests.length === 0 ? (
          <p className="text-sm text-slate-500">Квестів клану поки немає.</p>
        ) : (
          <ul className="space-y-3">
            {territory.quests.map((quest) => {
              const share = quest.target === 0 ? 1 : Math.min(quest.amount / quest.target, 1);

              return (
                <li key={quest.key} className="space-y-1 text-sm">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className={quest.completed ? "text-emerald-700" : "text-slate-800"}>
                      {quest.completed && "✓ "}
                      {quest.displayName}
                    </span>
                    <span className="text-xs text-slate-500">
                      +{number(quest.clanPoints)} очок{quest.opensSlot && " · слот"}
                    </span>
                  </div>
                  <div className="h-1.5 overflow-hidden rounded-full bg-slate-100">
                    <div className="h-full bg-emerald-500" style={{ width: `${share * 100}%` }} />
                  </div>
                  <p className="text-xs text-slate-500">
                    {number(Math.min(quest.amount, quest.target))} / {number(quest.target)}
                  </p>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
        <h3 className="font-medium text-slate-800">Найбільший внесок</h3>
        {territory.contributors.length === 0 ? (
          <p className="text-sm text-slate-500">Ще ніхто нічого не приніс.</p>
        ) : (
          <ol className="space-y-1 text-sm">
            {territory.contributors.map((contributor, index) => (
              <li key={contributor.playerId} className="flex justify-between gap-2">
                <span className={contributor.playerId === myPlayerId ? "font-medium text-slate-800" : "text-slate-700"}>
                  {index + 1}. {contributor.name}
                </span>
                <span className="text-slate-600">{number(contributor.contribution)}</span>
              </li>
            ))}
          </ol>
        )}
      </section>
    </div>
  );
}
