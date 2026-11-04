using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>Що дав розподіл: закриті рівні й скільки очок лишилось у запасі.</summary>
    public record ClanAllocationResult(IReadOnlyList<string> LevelsCompleted, long UnallocatedPoints);

    /// <summary>
    /// Офіцер вкладає очки із запасу клану у вузол (рішення 04.11). Часткове вкладення лишається на вузлі,
    /// тож його можна докласти пізніше; надлишок понад потрібне вертається в запас.
    /// </summary>
    public record AllocateClanPointsCommand(Guid PlayerId, int Tier, string Node, int Points)
        : IRequest<ClanAllocationResult>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class AllocateClanPointsCommandHandler : IRequestHandler<AllocateClanPointsCommand, ClanAllocationResult>
    {
        private readonly IClanRepository _clans;
        private readonly IClanResearchRepository _research;
        private readonly IServerRepository _servers;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanTechnologies _technologies;
        private readonly ResearchSettlement _settlement;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AllocateClanPointsCommandHandler> _logger;

        public AllocateClanPointsCommandHandler(
            IClanRepository clans,
            IClanResearchRepository research,
            IServerRepository servers,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            ClanTechnologies technologies,
            ResearchSettlement settlement,
            TimeProvider timeProvider,
            ILogger<AllocateClanPointsCommandHandler> logger)
        {
            _clans = clans;
            _research = research;
            _servers = servers;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _technologies = technologies;
            _settlement = settlement;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ClanAllocationResult> Handle(AllocateClanPointsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clans.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

            clan.EnsureCan(request.PlayerId, ClanPermission.ManageTechnologies);

            var node = _technologies.Tree.Node(request.Node)
                ?? throw new EntityNotFoundException("Technology", request.Node);
            var tier = _technologies.Tree.Tier(request.Tier)
                ?? throw new EntityNotFoundException("Technology tier", request.Tier.ToString());

            // Рядка ще немає — клан не зробив жодного підношення, тож і очок у запасі нема
            var research = await _research.GetByClanAsync(clan.Id, cancellationToken)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanTechNotEnoughPoints,
                    $"Clan {clan.Id} has no points yet.", request.Points, 0L);

            var worldLevel = await _servers.GetLevelAsync(_serverContext.ServerId, cancellationToken);

            // У клану ратуші немає — її гейт дерево пропускає найбільшим рівнем
            switch (_technologies.Tree.Availability(research.Levels, request.Tier, node.Key, int.MaxValue, worldLevel))
            {
                case TechnologyAvailability.Maxed:
                    throw new RequirementNotMetException(RefusalReasons.ResearchMaxed, $"{node.Key} is maxed on tier {request.Tier}.");

                case TechnologyAvailability.TierGated:
                    throw new RequirementNotMetException(RefusalReasons.ClanTechTierLocked,
                        $"Tier {request.Tier} needs world level {tier.WorldLevel}.", request.Tier, tier.WorldLevel);

                case TechnologyAvailability.PreviousTierIncomplete:
                    throw new RequirementNotMetException(RefusalReasons.ResearchPreviousTier,
                        $"Tier {request.Tier - 1} of {node.Tab} is not finished.", request.Tier - 1);

                case TechnologyAvailability.PrerequisitesMissing:
                    var missing = node.Requires.Where(r => TechnologyTree.LevelOf(research.Levels, request.Tier, r) < 1);
                    throw new RequirementNotMetException(RefusalReasons.ResearchPrerequisites,
                        $"{node.Key} needs a level of its prerequisites first.", string.Join(",", missing));

                case TechnologyAvailability.UnknownNode:
                    throw new EntityNotFoundException("Technology", node.Key);
            }

            var completed = research.Allocate(new TechnologyKey(request.Tier, node.Key), request.Points, _technologies, now);

            // Новий рівень виробітку діє лише відтепер: кожне село клану фіксує вироблене за старою ставкою,
            // яку ще віддає БД. Сотні сіл в одній транзакції, але такий рівень закривається рідко
            if (completed.Count > 0 && node.Effects.Any(e => e.Stat == TechnologyStat.Production))
                foreach (var member in clan.Members)
                    await _settlement.SettleVillageAsync(member.PlayerId, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} allocated {Points} clan points to {Tier}:{Node}, {Levels} levels closed",
                request.PlayerId, request.Points, request.Tier, node.Key, completed.Count);

            return new ClanAllocationResult(completed.Select(c => c.Key.ToString()).ToList(), research.UnallocatedPoints);
        }
    }
}
