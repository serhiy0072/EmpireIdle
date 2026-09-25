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

        public DefenderAudience(
            IVillageRepository villageRepository,
            IClanStructureRepository structureRepository,
            IClanRepository clanRepository,
            IPlayerRepository playerRepository)
        {
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
                    if (village is null)
                        return [];

                    var clanId = await _clanRepository.GetClanIdByMemberAsync(village.PlayerId, cancellationToken);

                    return clanId is { } id
                        ? await _playerRepository.GetIdsByClanAsync(id, cancellationToken)
                        : [village.PlayerId];

                case MarchTargetType.ClanStructure:
                    var structure = await _structureRepository.GetByIdAsync(targetId, cancellationToken);

                    return structure is null
                        ? []
                        : await _playerRepository.GetIdsByClanAsync(structure.ClanId, cancellationToken);

                default:
                    return [];
            }
        }
    }
}
