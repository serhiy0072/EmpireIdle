using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Відкриває унікальну зброю героя або піднімає її рівень за шматки (GDD §6.4): 5 на відкриття,
    /// далі 5/10/20/40. Шматки лежать на самому герої — їх дають скрині зброї й магазин.
    /// </summary>
    public record UpgradeHeroWeaponCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class UpgradeHeroWeaponCommandHandler : IRequestHandler<UpgradeHeroWeaponCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UpgradeHeroWeaponCommandHandler> _logger;

        public UpgradeHeroWeaponCommandHandler(
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            HeroProgression progression,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<UpgradeHeroWeaponCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UpgradeHeroWeaponCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var maxLevel = _catalog.Config.HeroSettings.MaxWeaponLevel;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Чужий герой не відрізняється від неіснуючого
            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            var cost = _progression.NextWeaponCost(hero.WeaponLevel)
                ?? throw new RequirementNotMetException(RefusalReasons.HeroWeaponMaxed,
                    $"Hero {hero.Id} weapon is already +{hero.WeaponLevel}.", maxLevel);

            hero.UpgradeWeapon(cost, maxLevel, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} weapon reached +{Level} for {Cost} shards", hero.Id, hero.WeaponLevel, cost);
        }
    }
}
