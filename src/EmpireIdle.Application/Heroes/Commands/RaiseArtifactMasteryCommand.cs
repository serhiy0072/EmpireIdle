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
    /// Заточка коваля (GDD §9.12): спроба за золото з шансом. Успіх додає ранг бонусу — його стат
    /// і ступінь розігрує ArtifactRoller із сідом у журналі предмета. Невдача з'їдає золото, а на
    /// пізніх рангах може ще й зламати предмет; попередження про це — у клієнті.
    /// </summary>
    public record RaiseArtifactMasteryCommand(Guid PlayerId, Guid EquipmentId)
        : IRequest<MasteryOutcome>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RaiseArtifactMasteryCommandHandler : IRequestHandler<RaiseArtifactMasteryCommand, MasteryOutcome>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly MasteryRules _rules;
        private readonly IRandomSource _random;
        private readonly ArtifactRoller _roller;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RaiseArtifactMasteryCommandHandler> _logger;

        public RaiseArtifactMasteryCommandHandler(
            IInventoryRepository inventoryRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            MasteryRules rules,
            IRandomSource random,
            ArtifactRoller roller,
            GameCatalog catalog,
            ILogger<RaiseArtifactMasteryCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _rules = rules;
            _random = random;
            _roller = roller;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<MasteryOutcome> Handle(RaiseArtifactMasteryCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var equipment = _catalog.Config.Equipment;

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken);

            if (item is null || item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            // До списання золота: виставлений чи зламаний не можна ні прокачати, ні оплатити спробу
            item.EnsureNotOnMarket();
            item.EnsureNotBroken();

            if (item.Mastery >= equipment.MaxMastery)
                throw new RequirementNotMetException(RefusalReasons.EquipmentMaxMastery,
                    $"Artifact {item.Id} mastery is already {item.Mastery}.", equipment.MaxMastery);

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            if (!village.HasBuilding(equipment.ForgeBuildingKey))
                throw new RequirementNotMetException(RefusalReasons.BuildingRequired,
                    $"Mastery requires the {equipment.ForgeBuildingKey}.", _catalog.Building(equipment.ForgeBuildingKey).DisplayName);

            village.ChargeCost([new ResourceCost { Resource = "gold", Amount = _rules.Cost(item.Mastery) }], now);

            var outcome = _rules.Roll(item.Mastery, _random);

            if (outcome == MasteryOutcome.Broken)
                item.Break(now);

            if (outcome == MasteryOutcome.Success)
            {
                var seed = _random.Next(int.MaxValue);
                var rank = _roller.RollRank(_catalog.FindItem(item.ItemKey)?.ArtifactSlot, item.Mastery, item.Stats, seed);

                item.ApplyMasteryRank(rank.Position, rank.Stat, rank.Value, now);
                item.RecordRoll(item.Mastery, seed, now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Artifact {EquipmentId} mastery attempt: {Outcome}, now {Mastery}",
                item.Id, outcome, item.Mastery);

            return outcome;
        }
    }
}
