using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Heroes.Services
{
    /// <summary>
    /// Куди йдуть осколки героя (GDD §6.1). Правило одне для всіх джерел — банер, дублікат,
    /// нагорода: осколки копляться на героя, а якщо його зірки вже заповнені — стають
    /// універсальними осколками його рідкості. Розкидане по джерелах, воно розійшлося б.
    /// </summary>
    public sealed class HeroShardBank
    {
        /// <summary>Тип предмета-універсального осколка; рідкість осколка — рідкість предмета.</summary>
        public const string UniversalShardType = "universalshard";

        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public HeroShardBank(IHeroRepository heroRepository, IInventoryRepository inventoryRepository,
            IServerContext serverContext, GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <summary>Додає осколки героя; надлишок прокачаного героя — в універсальні його рідкості.</summary>
        public async Task AddAsync(Guid playerId, string heroKey, int count, CancellationToken cancellationToken)
        {
            if (count < 1)
                return;

            var config = _catalog.Hero(heroKey);
            var owned = await _heroRepository.GetByKeyAsync(playerId, heroKey, cancellationToken);

            if (owned is not null && owned.StarParts >= _catalog.Config.HeroSettings.MaxStarParts)
            {
                await AddUniversalAsync(playerId, config.Rank, count, cancellationToken);
                return;
            }

            var progress = await GetOrCreateShardsAsync(playerId, heroKey, cancellationToken);
            progress.Add(count);
        }

        /// <summary>Лічильник осколків героя «знайти або створити».</summary>
        public async Task<HeroShardProgress> GetOrCreateShardsAsync(Guid playerId, string heroKey,
            CancellationToken cancellationToken)
        {
            var progress = await _heroRepository.GetShardsAsync(playerId, heroKey, cancellationToken);

            if (progress is not null)
                return progress;

            progress = new HeroShardProgress(Guid.NewGuid(), playerId, _serverContext.ServerId, heroKey);
            await _heroRepository.AddShardsAsync(progress, cancellationToken);

            return progress;
        }

        /// <summary>Додає універсальні осколки рідкості в інвентар.</summary>
        public async Task AddUniversalAsync(Guid playerId, Rarity rarity, int count, CancellationToken cancellationToken)
        {
            var key = UniversalItemKey(rarity);
            var stack = await _inventoryRepository.GetItemAsync(playerId, key, cancellationToken);

            if (stack is null)
                await _inventoryRepository.AddItemAsync(new PlayerItem(Guid.NewGuid(), playerId, key, count), cancellationToken);
            else
                stack.Add(count);
        }

        /// <summary>Ключ предмета-універсального осколка рідкості. Валідатор гарантує, що він є для кожної рідкості ростеру.</summary>
        public string UniversalItemKey(Rarity rarity)
            => _catalog.Config.Items.FirstOrDefault(i => i.Type == UniversalShardType && i.Rarity == rarity)?.Key
               ?? throw new InvalidOperationException($"No universal shard item for rarity {rarity}.");
    }
}
