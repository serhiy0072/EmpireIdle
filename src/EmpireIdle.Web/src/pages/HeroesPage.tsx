import { useState } from "react";
import { Link } from "react-router-dom";
import ErrorBanner from "../components/ErrorBanner";
import HeroCard from "../components/HeroCard";
import HeroDetails from "../components/HeroDetails";
import ShardsPanel from "../components/ShardsPanel";
import { useSession } from "../hooks/useSession";
import {
  useAppointLeader,
  useBuyShards,
  useEvolveHero,
  useHealHero,
  useHeroes,
  useLevelUpHero,
  useResetHeroLevel,
  useSummonHero,
} from "../lib/queries/heroes";

export default function HeroesPage() {
  const session = useSession();
  const playerId = session?.playerId ?? "";

  const heroes = useHeroes(playerId);
  const levelUp = useLevelUpHero(playerId);
  const evolve = useEvolveHero(playerId);
  const appointLeader = useAppointLeader(playerId);
  const heal = useHealHero(playerId);
  const summon = useSummonHero(playerId);
  const buyShards = useBuyShards(playerId);
  const resetLevel = useResetHeroLevel(playerId);

  const [selectedId, setSelectedId] = useState<string | null>(null);

  if (heroes.isPending) {
    return <p className="text-slate-500">Завантаження героїв…</p>;
  }

  if (heroes.isError) {
    return <ErrorBanner error={heroes.error} />;
  }

  const busy =
    levelUp.isPending ||
    evolve.isPending ||
    appointLeader.isPending ||
    heal.isPending ||
    summon.isPending ||
    buyShards.isPending ||
    resetLevel.isPending;

  const failure =
    levelUp.error ??
    evolve.error ??
    appointLeader.error ??
    heal.error ??
    summon.error ??
    buyShards.error ??
    resetLevel.error;

  const selected = heroes.data.heroes.find((hero) => hero.id === selectedId) ?? heroes.data.heroes[0] ?? null;
  const free = heroes.data.heroes.filter((hero) => hero.state === "Idle").length;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h1 className="text-xl font-medium text-slate-800">Герої</h1>
        <p className="text-sm text-slate-500">
          Досвід героїв: {heroes.data.experience.toLocaleString("uk-UA")} · вільних: {free} · стеля маршів:{" "}
          {heroes.data.marchCapacity} ·{" "}
          <Link to="/heroes/codex" className="text-emerald-700 hover:underline">
            кодекс
          </Link>
        </p>
      </div>

      <ErrorBanner error={failure} />

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div className="space-y-4">
          {heroes.data.heroes.length === 0 ? (
            <p className="text-sm text-slate-500">Героїв ще немає — купіть уламки звичайного героя за золото або крутіть банери.</p>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              {heroes.data.heroes.map((hero) => (
                <HeroCard
                  key={hero.id}
                  hero={hero}
                  selected={selected?.id === hero.id}
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
            experience={heroes.data.experience}
            busy={busy}
            onLevelUp={() => levelUp.mutate({ heroId: selected.id, levels: 1 })}
            onResetLevel={() => resetLevel.mutate(selected.id)}
            onEvolve={() => evolve.mutate(selected.id)}
            onAppointLeader={() => appointLeader.mutate(selected.id)}
            onHeal={() => heal.mutate(selected.id)}
          />
        )}
      </div>
    </div>
  );
}
