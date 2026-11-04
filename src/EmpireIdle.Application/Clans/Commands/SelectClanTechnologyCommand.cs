using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Офіцер обирає, яку технологію клан вивчає (рішення 28.10.2026). Зміна цілі не губить
    /// накопичених очок: вони чекають на вузлі, доки клан до нього повернеться.
    /// </summary>
    public record SelectClanTechnologyCommand(Guid PlayerId, int Tier, string Node)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class SelectClanTechnologyCommandHandler : IRequestHandler<SelectClanTechnologyCommand>
    {
        private readonly IClanRepository _clans;
        private readonly IClanResearchRepository _research;
        private readonly IServerRepository _servers;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanTechnologies _technologies;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SelectClanTechnologyCommandHandler> _logger;

        public SelectClanTechnologyCommandHandler(
            IClanRepository clans,
            IClanResearchRepository research,
            IServerRepository servers,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            ClanTechnologies technologies,
            TimeProvider timeProvider,
            ILogger<SelectClanTechnologyCommandHandler> logger)
        {
            _clans = clans;
            _research = research;
            _servers = servers;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _technologies = technologies;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(SelectClanTechnologyCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _clans.GetByMemberAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

            clan.EnsureCan(request.PlayerId, ClanPermission.ManageTechnologies);

            var node = _technologies.Tree.Node(request.Node)
                ?? throw new EntityNotFoundException("Technology", request.Node);
            var tier = _technologies.Tree.Tier(request.Tier)
                ?? throw new EntityNotFoundException("Technology tier", request.Tier.ToString());

            var research = await _research.GetByClanAsync(clan.Id, cancellationToken);
            if (research is null)
            {
                research = new ClanResearch(Guid.NewGuid(), clan.Id, _serverContext.ServerId, now);
                await _research.AddAsync(research, cancellationToken);
            }

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

            var key = new TechnologyKey(request.Tier, node.Key);
            research.SelectTarget(key, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} set clan {ClanId} technology target to {Key}",
                request.PlayerId, clan.Id, key);
        }
    }
}
