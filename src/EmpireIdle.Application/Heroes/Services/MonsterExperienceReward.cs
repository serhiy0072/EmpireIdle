using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Heroes.Services
{
    /// <summary>
    /// Досвід героїв за перемогу над монстром (GDD §6.1): PvE-марш — полювання чи приручення —
    /// поповнює спільний запас гравця пропорційно рівню монстра. Ліміту немає: полювання не витрачає енергії
    /// й обмежене лише часом маршу.
    /// </summary>
    public sealed class MonsterExperienceReward
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;
        private readonly GameCatalog _catalog;
        private readonly PlayerTechnologies _technologies;

        public MonsterExperienceReward(IHeroRepository heroRepository, IServerContext serverContext, GameCatalog catalog,
            PlayerTechnologies technologies)
        {
            _technologies = technologies;
            _heroRepository = heroRepository;
            _serverContext = serverContext;
            _catalog = catalog;
        }

        /// <summary>Нараховує досвід за переможеного монстра; повертає, скільки нараховано.</summary>
        public async Task<long> GrantAsync(Guid playerId, int monsterLevel, DateTime utcNow, CancellationToken cancellationToken)
        {
            var settings = _catalog.Config.HeroSettings;
            var basic = settings.MonsterExperiencePerLevel * Math.Max(1, monsterLevel);

            if (basic <= 0)
                return 0;

            // Технології наставництва додають досвіду лише за бій (рішення 28.10.2026)
            var amount = BattleExperience.Boosted(basic,
                (await _technologies.ForPlayerAsync(playerId, cancellationToken)).Of(TechnologyStat.HeroExperience));

            var pool = await _heroRepository.GetOrCreateExperienceAsync(playerId, _serverContext.ServerId, cancellationToken);

            pool.Add(amount);

            return amount;
        }
    }
}
