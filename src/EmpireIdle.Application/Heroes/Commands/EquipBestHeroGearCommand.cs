using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// «Швидке використання» (GDD §6.1): у кожен слот героя — найкраще вільне спорядження,
    /// якщо воно краще за вдягнене. З інших героїв нічого не знімає.
    /// </summary>
    public record EquipBestHeroGearCommand(Guid PlayerId, Guid HeroId)
        : IRequest<int>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник EquipBestHeroGearCommand. Повертає, скільки предметів вдягнено.
    ///
    /// Кандидатів читає без трекінгу, а вдягає лише обраних: ростер спорядження буває
    /// довгим, а змінюється щонайбільше по предмету на слот.
    /// </summary>
    public sealed class EquipBestHeroGearCommandHandler : IRequestHandler<EquipBestHeroGearCommand, int>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly EquipmentFit _fit;
        private readonly ILogger<EquipBestHeroGearCommandHandler> _logger;

        public EquipBestHeroGearCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            GameCatalog catalog,
            EquipmentFit fit,
            ILogger<EquipBestHeroGearCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _fit = fit;
            _logger = logger;
        }

        public async Task<int> Handle(EquipBestHeroGearCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Чужий герой не відрізняється від неіснуючого
            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Спорядження міняють удома: герой у дорозі не переодягається
            if (hero.StationedGarrisonId is null)
                throw new RequirementNotMetException(RefusalReasons.HeroOnTheMove, $"Hero {hero.Id} is on the move.");

            var owned = await _inventoryRepository.GetEquipmentAsync(request.PlayerId, cancellationToken);
            var best = _fit.BestFree(_catalog.FindHero(hero.HeroKey)?.Class, owned);
            var equipped = await _inventoryRepository.GetEquippedAsync(hero.Id, cancellationToken);
            var changed = 0;

            foreach (var ((slot, index), candidate) in best)
            {
                var occupant = equipped.FirstOrDefault(e => e.Slot == slot && e.SlotIndex == index);

                if (!EquipmentFit.IsBetter(candidate, occupant))
                    continue;

                // Кандидат прочитаний без трекінгу — вдягаємо відстежений екземпляр
                var item = await _inventoryRepository.GetEquipmentByIdAsync(candidate.Id, cancellationToken)
                    ?? throw new EntityNotFoundException("Equipment", candidate.Id.ToString());

                occupant?.Unequip(now);
                item.EquipTo(hero.Id, index, now);
                changed++;
            }

            if (changed > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} quick-equipped {Count} items", hero.Id, changed);

            return changed;
        }
    }
}
