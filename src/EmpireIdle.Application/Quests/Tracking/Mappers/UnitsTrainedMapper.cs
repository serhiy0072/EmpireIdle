using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Tracking.Mappers
{
    /// <summary>Тренування юнітів. Гарнізон не знає гравця — резолвимо через село.</summary>
    public class UnitsTrainedMapper : QuestSignalMapper<UnitsTrained>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly ILogger<UnitsTrainedMapper> _logger;

        public UnitsTrainedMapper(IVillageRepository villageRepository, ILogger<UnitsTrainedMapper> logger)
        {
            _villageRepository = villageRepository;
            _logger = logger;
        }

        /// <inheritdoc/>
        protected override async Task<QuestSignal?> Map(UnitsTrained e, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByIdAsync(e.VillageId, cancellationToken);

            if (village is null)
            {
                _logger.LogWarning("Quest signal dropped: village {VillageId} not found for UnitsTrained.", e.VillageId);
                return null;
            }

            return new QuestSignal(village.PlayerId, nameof(UnitsTrained), e.UnitType, e.Count, null);
        }
    }
}
