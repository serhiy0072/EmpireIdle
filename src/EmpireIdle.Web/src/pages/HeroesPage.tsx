import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import HeroCard from "../components/HeroCard";
import HeroDetails from "../components/HeroDetails";
import ShardsPanel from "../components/ShardsPanel";
import { useNow } from "../hooks/useNow";
import { useSession } from "../hooks/useSession";
import { describeError } from "../lib/errorMessages";
import { queryKeys } from "../lib/queryKeys";
import {
  useAppointLeader,
  useBuyShards,
  useEvolveHero,
  useHealHero,
  useHeroes,
  useLevelUpHero,
  useSummonHero,
} from "../lib/queries/heroes";

function countdown(completesAt: string, now: number): string | null {
  const seconds = Math.round((Date.parse(completesAt) - now) / 1_000);

  if (seconds <= 0) return null;

  const minutes = Math.floor(seconds / 60);

  return minutes > 0 ? `${minutes} хв ${seconds % 60} с` : `${seconds} с`;
}

export default function HeroesPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";
  const now = useNow();
  const queryClient = useQueryClient();

  const heroes = useHeroes(playerId);
  const levelUp = useLevelUpHero(playerId);
  const evolve = useEvolveHero(playerId);
  const appointLeader = useAppointLeader(playerId);
  const heal = useHealHero(playerId);
  const summon = useSummonHero(playerId);
  const buyShards = useBuyShards(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  const order = heroes.data?.activeOrder ?? null;
  const orderLeft = order === null ? null : countdown(order.completesAt, now);

  // Чергу завершує сканер на сервері: коли відлік вийшов, перезапитуємо ростер
  useEffect(() => {
    if (order !== null && orderLeft === null) {
      void queryClient.invalidateQueries({ queryKey: queryKeys.heroes(playerId) });
    }
  }, [order, orderLeft, playerId, queryClient]);

  if (heroes.isPending) {
    return <p className="text-slate-500">Завантаження героїв…</p>;
  }

  if (heroes.isError) {
    return <p className="text-red-600">{describeError(heroes.error)}</p>;
  }

  const busy =
    levelUp.isPending || evolve.isPending || appointLeader.isPending || heal.isPending || summon.isPending || buyShards.isPending;

  const failure =
    levelUp.error ?? evolve.error ?? appointLeader.error ?? heal.error ?? summon.error ?? buyShards.error;

  const selected = heroes.data.heroes.find((hero) => hero.id === selectedId) ?? heroes.data.heroes[0] ?? null;
  const free = heroes.data.heroes.filter((hero) => hero.state === "Idle").length;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Герої</h1>
        <p className="text-sm text-slate-500">
          Вільних: {free} · стеля маршів: {heroes.data.marchCapacity}
        </p>
      </div>

      {failure !== null && failure !== undefined && (
        <p className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{describeError(failure)}</p>
      )}

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div className="space-y-4">
          {heroes.data.heroes.length === 0 ? (
            <p className="text-sm text-slate-500">Героїв ще немає — призовіть першого з уламків.</p>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {heroes.data.heroes.map((hero) => (
                <HeroCard
                  key={hero.id}
                  hero={hero}
                  selected={selected?.id === hero.id}
                  levelingUntil={order !== null && order.heroId === hero.id ? orderLeft : null}
                  onSelect={() => setSelectedId(hero.id)}
                />
              ))}
            </div>
          )}

          <section className="space-y-2">
            <h2 className="text-sm font-medium uppercase tracking-wide text-slate-500">Уламки</h2>
            <ShardsPanel
              shards={heroes.data.shards}
              busy={busy}
              onBuy={(heroKey, count) => buyShards.mutate({ heroKey, count })}
              onSummon={(heroKey) => summon.mutate(heroKey)}
            />
          </section>
        </div>

        {selected !== null && (
          <HeroDetails
            hero={selected}
            queueBusy={order !== null}
            busy={busy}
            onLevelUp={() => levelUp.mutate(selected.id)}
            onEvolve={() => evolve.mutate(selected.id)}
            onAppointLeader={() => appointLeader.mutate(selected.id)}
            onHeal={() => heal.mutate(selected.id)}
          />
        )}
      </div>
    </div>
  );
}
