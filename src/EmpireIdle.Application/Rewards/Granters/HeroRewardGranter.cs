using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>
    /// Видає героя: нового додає в ростер, дубліката зараховує в сузір'я.
    ///
    /// Той самий шлях обслуговує квести, віхи й банери — саме тому призов
    /// не живе в хендлері квесту. Стартовий герой за ратушу 3 приходить сюди ж.
    /// </summary>
    public class HeroRewardGranter : IRewardGranter
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public HeroRewardGranter(IHeroRepository heroRepository, IServerContext serverContext, GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <inheritdoc/>
        public string RewardType => "Hero";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"Hero reward from '{context.Reference}' has no Key.");

            // Кидає, якщо героя немає в каталозі — краще впасти при видачі,
            // ніж створити рядок, для якого немає ні стат, ні картки
            _catalog.Hero(key);

            var existing = await _heroRepository.GetByKeyAsync(context.PlayerId, key, cancellationToken);

            if (existing is null)
            {
                await _heroRepository.AddAsync(
                    new Hero(Guid.NewGuid(), context.PlayerId, _serverContext.ServerId, key, context.UtcNow),
                    cancellationToken);

                return;
            }

            // Дублікат іде в сузір'я. Понад стелю зараз згорає: правила конвертації
            // надлишку визначаються у фазі 25 разом із гача, і поки їх немає,
            // видати щось інше замість героя ми не можемо.
            existing.TryAddConstellation(_catalog.Config.HeroSettings.MaxConstellation, context.UtcNow);
        }
    }
}
