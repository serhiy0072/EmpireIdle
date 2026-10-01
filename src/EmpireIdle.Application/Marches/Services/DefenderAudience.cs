using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Marches.Services
{
    /// <summary>
    /// Кого тривожить напад на ціль: власника села й увесь його клан (поза кланом — лише
    /// власника), для споруди — клан-власник. Одне правило для тривоги й для її зняття.
    /// </summary>
    public sealed class DefenderAudience
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;

        public DefenderAudience(
            IVillageRepository villageRepository,
            IClanStructureRepository structureRepository,
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _structureRepository = structureRepository;
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
        }

        /// <returns>Порожньо — ціль зникла, тривожити нікого.</returns>
        public async Task<IReadOnlyCollection<Guid>> ResolveAsync(MarchTargetType targetType, Guid targetId,
            CancellationToken cancellationToken)
        {
            switch (targetType)
            {
                case MarchTargetType.Village:
                    var village = await _villageRepository.GetByIdAsync(targetId, cancellationToken);

                    return village is null ? [] : await OwnerAndClanAsync(village.PlayerId, cancellationToken);

                // Табір — армія гравця: тривога та сама, що й за його село (§2.5)
                case MarchTargetType.Camp:
                    var camp = await _marchRepository.GetByIdAsync(targetId, cancellationToken);
                    var campGarrison = camp is null
                        ? null
                        : await _garrisonRepository.GetByIdAsync(camp.GarrisonId, cancellationToken);
                    var campHome = campGarrison is null
                        ? null
                        : await _villageRepository.GetByIdAsync(campGarrison.VillageId, cancellationToken);

                    return campHome is null ? [] : await OwnerAndClanAsync(campHome.PlayerId, cancellationToken);

                case MarchTargetType.ClanStructure:
                    var structure = await _structureRepository.GetByIdAsync(targetId, cancellationToken);

                    return structure is null
                        ? []
                        : await _playerRepository.GetIdsByClanAsync(structure.ClanId, cancellationToken);

                default:
                    return [];
            }
        }

        private async Task<IReadOnlyCollection<Guid>> OwnerAndClanAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var clanId = await _clanRepository.GetClanIdByMemberAsync(playerId, cancellationToken);

            return clanId is { } id
                ? await _playerRepository.GetIdsByClanAsync(id, cancellationToken)
                : [playerId];
        }
    }
}
