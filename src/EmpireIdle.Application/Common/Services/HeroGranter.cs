using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Видає героя незалежно від джерела: квест, віха, призов за осколки.
    ///
    /// Правило одне на всіх (GDD §6.1) — новий герой іде в ростер, а дублікат стає осколками
    /// (стільки, скільки коштує призов), які банк осколків розкладає далі. Тримається в одному
    /// місці навмисно: продубльоване, воно розійшлося б на першій же зміні балансу.
    /// </summary>
    public class HeroGranter
    {
        private readonly IHeroRepository _heroRepository;
        private readonly HeroShardBank _shards;
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public HeroGranter(
            IHeroRepository heroRepository,
            HeroShardBank shards,
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IServerContext serverContext,
            GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _shards = shards;
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <param name="reference">Джерело — ключ квесту або призову. Іде в журнал гаманця.</param>
        public async Task GrantAsync(Guid playerId, string heroKey, string reference,
            DateTime utcNow, CancellationToken cancellationToken = default)
        {
            // Кидає, якщо героя немає в каталозі — краще впасти при видачі,
            // ніж створити рядок, для якого немає ні стат, ні картки
            var config = _catalog.FindHero(heroKey)
                ?? throw new InvalidOperationException($"Hero '{heroKey}' from '{reference}' is not in the catalog.");

            var existing = await _heroRepository.GetByKeyAsync(playerId, heroKey, cancellationToken);

            if (existing is null)
            {
                // Герой оселяється в гарнізоні одразу. Інакше новачок отримує
                // нагороду, бачить нуль ефекту й мусить сам знайти кнопку
                var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken)
                    ?? throw new InvalidOperationException($"Village not found for player {playerId}.");

                var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                    ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

                var leader = await _heroRepository.GetLeaderAsync(garrison.Id, playerId, cancellationToken);

                var hero = new Hero(Guid.NewGuid(), playerId, _serverContext.ServerId, heroKey, garrison.Id,
                    asLeader: leader is null, utcNow, config.NativeTier);

                await _heroRepository.AddAsync(hero, cancellationToken);

                return;
            }

            // Дублікат — це осколки на зірки; прокачаному героєві банк віддасть їх універсальними
            await _shards.AddAsync(playerId, heroKey, _catalog.Config.HeroSettings.SummonShards, cancellationToken);
        }
    }
}
