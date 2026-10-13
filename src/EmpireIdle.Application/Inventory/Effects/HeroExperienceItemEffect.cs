using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>
    /// Баночка досвіду (GDD §6.1): відкрита — додає свій досвід у пул гравця, а не конкретному героєві.
    /// Кому його віддати, гравець вирішує потім, коли піднімає рівень.
    /// </summary>
    public class HeroExperienceItemEffect : IItemEffect
    {
        public string ItemType => "heroxp";

        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;

        public HeroExperienceItemEffect(IHeroRepository heroRepository, IServerContext serverContext)
        {
            _heroRepository = heroRepository;
            _serverContext = serverContext;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            var pool = await _heroRepository.GetOrCreateExperienceAsync(context.PlayerId, _serverContext.ServerId, cancellationToken);
            pool.Add(checked(context.Config.HeroExperience * context.Count));
        }
    }
}
