using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Heroes.Services
{
    /// <summary>
    /// Досвід героїв за перемогу над монстром (GDD §6.1): PvE-марш — полювання чи приручення —
    /// поповнює спільний запас гравця пропорційно рівню монстра, у межах денного ліміту перемог.
    /// </summary>
    public sealed class MonsterExperienceReward
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;

        public MonsterExperienceReward(IHeroRepository heroRepository, IServerContext serverContext, GameCatalog catalog)
        {
            _heroRepository = heroRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <summary>Нараховує досвід за переможеного монстра; повертає, скільки нараховано (0 — понад ліміт).</summary>
        public async Task<long> GrantAsync(Guid playerId, int monsterLevel, DateTime utcNow, CancellationToken cancellationToken)
        {
            var settings = _catalog.Config.HeroSettings;
            var amount = settings.MonsterExperiencePerLevel * Math.Max(1, monsterLevel);

            if (amount <= 0)
                return 0;

            var pool = await _heroRepository.GetOrCreateExperienceAsync(playerId, _serverContext.ServerId, cancellationToken);

            return pool.AddForMonster(amount, settings.MonsterExperienceDailyLimit, utcNow);
        }
    }
}
