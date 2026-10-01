using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Territory.Services
{
    /// <summary>
    /// Прибирає споруду з карти: гарнізон іде додому, клітина звільняється,
    /// бонус території зникає одразу. Спільне для знесення кланом і руйнування
    /// в бою — правило одне, і дублювати його не можна.
    /// Не зберігає: транзакцією володіє той, хто викликав.
    /// </summary>
    public sealed class ClanStructureRemover
    {
        private readonly IClanStructureRepository _structureRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMapRepository _mapRepository;
        private readonly ReinforcementReturner _returner;
        private readonly ILogger<ClanStructureRemover> _logger;

        public ClanStructureRemover(
            IClanStructureRepository structureRepository,
            IGarrisonRepository garrisonRepository,
            IMapRepository mapRepository,
            ReinforcementReturner returner,
            ILogger<ClanStructureRemover> logger)
        {
            _structureRepository = structureRepository;
            _garrisonRepository = garrisonRepository;
            _mapRepository = mapRepository;
            _returner = returner;
            _logger = logger;
        }

        public async Task RemoveAsync(ClanStructure structure, DateTime utcNow, CancellationToken cancellationToken)
        {
            var garrison = await _garrisonRepository.GetByIdAsync(structure.GarrisonId, cancellationToken);

            if (garrison is not null)
            {
                // Спершу розпускаємо: марші додому рахують шлях від споруди, поки вона ще є
                var sent = await _returner.ReturnAllFromStructureAsync(garrison, utcNow, cancellationToken);

                _garrisonRepository.Remove(garrison);

                _logger.LogInformation("Structure {StructureId} garrison sent home in {Count} marches", structure.Id, sent);
            }

            var cell = await _mapRepository.GetByOccupantAsync(MapOccupantType.ClanStructure, structure.Id, cancellationToken);

            if (cell is not null)
                _mapRepository.Remove(cell);

            _structureRepository.Remove(structure);
        }
    }
}
