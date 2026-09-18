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
    /// <summary>Прокачати артефакт на один рівень.</summary>
    public record EnhanceArtifactCommand(Guid PlayerId, Guid EquipmentId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник EnhanceArtifactCommand.
    ///
    /// Артефакт не ламається й не провалюється: він здобувається в данжі,
    /// а не купується, і другий шанс коштує забігу. Уся випадковість тут
    /// не в тому, чи вийде, а в тому, що саме випаде.
    ///
    /// Сід генерується тут і лягає в журнал предмета — ролл відтворюваний.
    /// </summary>
    public sealed class EnhanceArtifactCommandHandler : IRequestHandler<EnhanceArtifactCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly EnhancementRules _rules;
        private readonly ArtifactRoller _roller;
        private readonly IRandomSource _random;
        private readonly GameCatalog _catalog;
        private readonly ILogger<EnhanceArtifactCommandHandler> _logger;

        public EnhanceArtifactCommandHandler(
            IInventoryRepository inventoryRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            EnhancementRules rules,
            ArtifactRoller roller,
            IRandomSource random,
            GameCatalog catalog,
            ILogger<EnhanceArtifactCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _rules = rules;
            _roller = roller;
            _random = random;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(EnhanceArtifactCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken)
                ?? throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.Slot != EquipmentSlot.Artifact)
                throw new RequirementNotMetException($"Equipment {item.Id} is not an artifact.");

            var equipment = _catalog.Config.Equipment;

            if (item.EnhancementLevel >= equipment.MaxEnhancement)
                throw new RequirementNotMetException(
                    $"Artifact {item.Id} is already at the ceiling of +{equipment.MaxEnhancement}.");

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            if (!village.HasBuilding(equipment.ForgeBuildingKey))
                throw new RequirementNotMetException($"Upgrading requires the {equipment.ForgeBuildingKey}.");

            village.ChargeCost(
                [new ResourceCost { Resource = "gold", Amount = _rules.EnhanceCost(item.EnhancementLevel) }], now);

            item.Enhance(now);

            var seed = _random.Next(int.MaxValue);

            var roll = _roller.RollForLevel(
                item.EnhancementLevel, item.Rarity,
                item.Stats.Select(s => s.StatKey).ToList(), seed);

            foreach (var (stat, value) in roll.Added)
                item.AddStat(stat, value, now);

            foreach (var (stat, delta) in roll.Raised)
                item.RaiseStat(stat, delta, now);

            item.RecordRoll(item.EnhancementLevel, seed, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Artifact {EquipmentId} upgraded to +{Level}: {Added} added, {Raised} raised (seed {Seed})",
                item.Id, item.EnhancementLevel, roll.Added.Count, roll.Raised.Count, seed);
        }
    }
}
