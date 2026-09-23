using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Вдягнути спорядження на героя.
    /// </summary>
    /// <param name="SlotIndex">
    /// Номер артефактного слота (0..ArtifactSlots-1). Для зброї ігнорується:
    /// слот один.
    /// </param>
    public record EquipHeroItemCommand(Guid PlayerId, Guid HeroId, Guid EquipmentId, int SlotIndex = 0)
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
        private readonly ILogger<EquipHeroItemCommandHandler> _logger;

        public EquipHeroItemCommandHandler(
            IHeroRepository heroRepository,
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            GameCatalog catalog,
            ILogger<EquipHeroItemCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
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

            var slotIndex = ResolveSlotIndex(item.Slot, request.SlotIndex);

            EnsureFits(hero.HeroKey, item.Slot, itemConfig);

            var equipped = await _inventoryRepository.GetEquippedAsync(hero.Id, cancellationToken);

            // Уже стоїть у цьому ж слоті — нічого не робимо: команда ідемпотентна
            if (item.EquippedByHeroId == hero.Id && item.SlotIndex == slotIndex)
                return;

            // Знімаємо з попереднього носія або зі старого слота
            if (item.EquippedByHeroId is not null)
                item.Unequip(now);

            // Той, хто стоїть у цільовому слоті, зараз буде знятий — дублікатом він не рахується.
            // Перевірка до будь-якої мутації: відмова не лишає предмет знятим у трекері
            var occupant = equipped.FirstOrDefault(e => e.Slot == item.Slot && e.SlotIndex == slotIndex);

            if (equipped.Any(e => e.Id != item.Id && e.Id != occupant?.Id && e.ItemKey == item.ItemKey))
                throw new AlreadyExistsException(RefusalReasons.EquipmentAlreadyEquipped, "Equipped item", item.ItemKey,
                    _catalog.FindItem(item.ItemKey)?.DisplayName ?? item.ItemKey);

            // Знімаємо з попереднього носія або зі старого слота
            if (item.EquippedByHeroId is not null)
                item.Unequip(now);

            occupant?.Unequip(now);

            item.EquipTo(hero.Id, slotIndex, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Equipment {EquipmentId} equipped on hero {HeroId} in {Slot}[{SlotIndex}]",
                item.Id, hero.Id, item.Slot, slotIndex);
        }

        /// <summary>
        /// Зброя завжди в нульовому слоті, артефакт — у межах конфіга.
        /// Номер поза межами це помилка клієнта, а не мовчазне підставляння нуля.
        /// </summary>
        private int ResolveSlotIndex(EquipmentSlot slot, int requested)
        {
            if (slot == EquipmentSlot.Weapon)
                return 0;

            var slots = _catalog.Config.Equipment.ArtifactSlots;

            if (requested < 0 || requested >= slots)
                throw new RequirementNotMetException(
                    $"Artifact slot {requested} is outside 0..{slots - 1}.");

            return requested;
        }

        /// <summary>
        /// Зброя підходить за класом героя. Порожній список класів у конфігу
        /// означає «підходить усім», а не «нікому»: більшість артефактів
        /// саме такі.
        /// </summary>
        private void EnsureFits(string heroKey, EquipmentSlot slot, ItemConfig itemConfig)
        {
            if (slot != EquipmentSlot.Weapon || itemConfig.WeaponClasses.Count == 0)
                return;

            var heroClass = _catalog.FindHero(heroKey)?.Class;

            if (heroClass is null || !itemConfig.WeaponClasses.Contains(heroClass))
                throw new RequirementNotMetException(RefusalReasons.EquipmentClassMismatch,
                    $"Weapon '{itemConfig.Key}' does not fit a {heroClass ?? "unknown"} hero.", itemConfig.DisplayName);
        }
    }
}
