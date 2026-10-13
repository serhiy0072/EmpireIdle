using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Вдягнути артефакт на героя. Слот визначає сам предмет — слот свого типу
    /// (намисто, корона…). Зброя — частина героя, її не вдягають (GDD §6.4).
    /// </summary>
    public record EquipHeroItemCommand(Guid PlayerId, Guid HeroId, Guid EquipmentId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник EquipHeroItemCommand.
    ///
    /// Зайнятий слот не відмова, а заміна: попередній предмет знімається
    /// в тій самій транзакції. Інакше частковий унікальний індекс відкинув
    /// би вставку, і гравець отримав би 409 замість очікуваної зміни.
    /// </summary>
    public sealed class EquipHeroItemCommandHandler : IRequestHandler<EquipHeroItemCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly EquipmentFit _fit;
        private readonly ILogger<EquipHeroItemCommandHandler> _logger;

        public EquipHeroItemCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            GameCatalog catalog,
            EquipmentFit fit,
            ILogger<EquipHeroItemCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _fit = fit;
            _logger = logger;
        }

        public async Task Handle(EquipHeroItemCommand request, CancellationToken cancellationToken)
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

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken)
                ?? throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            var itemConfig = _catalog.Items.GetValueOrDefault(item.ItemKey)
                ?? throw new EntityNotFoundException("Item", item.ItemKey);

            var slotIndex = _fit.SlotIndexOf(itemConfig);

            var equipped = await _inventoryRepository.GetEquippedAsync(hero.Id, cancellationToken);

            // Уже стоїть у цьому ж слоті — нічого не робимо: команда ідемпотентна
            if (item.EquippedByHeroId == hero.Id && item.SlotIndex == slotIndex)
                return;

            // Той, хто стоїть у цільовому слоті, зараз буде знятий. Окремої заборони
            // дублікатів не треба: однаковий ключ — однаковий тип слота, тож другий
            // такий самий артефакт просто замінює першого
            var occupant = equipped.FirstOrDefault(e => e.SlotIndex == slotIndex);

            // Знімаємо з попереднього носія або зі старого слота
            if (item.EquippedByHeroId is not null)
                item.Unequip(now);

            occupant?.Unequip(now);

            item.EquipTo(hero.Id, slotIndex, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Equipment {EquipmentId} equipped on hero {HeroId} in {Slot}[{SlotIndex}]",
                item.Id, hero.Id, item.Slot, slotIndex);
        }
    }
}
