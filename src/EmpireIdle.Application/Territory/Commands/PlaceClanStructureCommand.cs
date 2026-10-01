using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Territory.Commands
{
    /// <summary>
    /// Закладає кланову споруду на вільній клітині (GDD §7.2). Платить клан очками
    /// вкладу; добудовують її марші учасників, а до того вона лише займає клітину.
    /// </summary>
    public record PlaceClanStructureCommand(Guid PlayerId, int X, int Y)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class PlaceClanStructureCommandHandler : IRequestHandler<PlaceClanStructureCommand, Guid>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanQuestRepository _clanQuestRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly WorldGeometry _geometry;
        private readonly TerrainGenerator _terrain;
        private readonly ClanTerritoryRules _rules;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PlaceClanStructureCommandHandler> _logger;

        public PlaceClanStructureCommandHandler(
            IClanRepository clanRepository,
            IClanStructureRepository structureRepository,
            IClanQuestRepository clanQuestRepository,
            IGarrisonRepository garrisonRepository,
            IMapRepository mapRepository,
            IServerRepository serverRepository,
            IServerContext serverContext,
            WorldGeometry geometry,
            TerrainGenerator terrain,
            ClanTerritoryRules rules,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<PlaceClanStructureCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _clanQuestRepository = clanQuestRepository;
            _garrisonRepository = garrisonRepository;
            _mapRepository = mapRepository;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _geometry = geometry;
            _terrain = terrain;
            _rules = rules;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(PlaceClanStructureCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Без причини для клієнта: у світі без території кнопки немає
            if (!_rules.Enabled)
                throw new RequirementNotMetException("Clan territory is not enabled in this world.");

            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanNotMember, "Only clan members can place structures.");

            clan.EnsureCan(request.PlayerId, ClanPermission.BuildStructures);

            var structures = await _structureRepository.GetByClanAsync(clan.Id, cancellationToken);
            var completedQuests = await _clanQuestRepository.GetCompletedKeysAsync(clan.Id, cancellationToken);
            var slots = _rules.SlotsFor(clan.Members.Count, completedQuests);

            if (structures.Count >= slots)
                throw new RequirementNotMetException(RefusalReasons.TerritoryNoFreeSlot,
                    $"All {slots} open structure slots are taken.", slots);

            var serverId = _serverContext.ServerId;
            var serverLevel = await _serverRepository.GetLevelAsync(serverId, cancellationToken);

            if (!_geometry.IsWithinFog(request.X, request.Y, serverLevel) || !_terrain.IsHabitable(serverId, request.X, request.Y))
                throw new RequirementNotMetException(RefusalReasons.TerritoryCellUnfit, "That cell cannot hold a clan structure.");

            // Гонку двох закладень на одну клітину розв'язує унікальний індекс карти
            if (await _mapRepository.IsOccupiedAsync(serverId, request.X, request.Y, cancellationToken))
                throw new AlreadyExistsException(RefusalReasons.TerritoryCellTaken, "Map cell", $"({request.X},{request.Y})");

            clan.SpendPoints(_rules.StructureCost, now);

            var structureId = Guid.NewGuid();
            var garrison = Garrison.ForStructure(Guid.NewGuid(), structureId, serverId);

            var structure = new ClanStructure(structureId, serverId, clan.Id, request.X, request.Y, garrison.Id,
                request.PlayerId, _rules.BuildDuration, now);

            await _garrisonRepository.AddAsync(garrison, cancellationToken);
            await _structureRepository.AddAsync(structure, cancellationToken);
            await _mapRepository.AddAsync(
                new MapCell(Guid.NewGuid(), serverId, request.X, request.Y, MapOccupantType.ClanStructure, structureId),
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan {ClanId} placed structure {StructureId} at ({X},{Y}), ready at {CompletesAt}",
                clan.Id, structureId, request.X, request.Y, structure.CompletesAt);

            return structureId;
        }
    }
}
