using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Research.Commands
{
    /// <summary>Почати наступний рівень вузла особистих технологій в Лабораторії (рішення 28.10.2026).</summary>
    public record StartResearchCommand(Guid PlayerId, int Tier, string Node)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class StartResearchCommandHandler : IRequestHandler<StartResearchCommand>
    {
        private readonly IVillageRepository _villages;
        private readonly IPlayerResearchRepository _research;
        private readonly IServerRepository _servers;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly PersonalTechnologies _technologies;
        private readonly PlayerTechnologies _bonuses;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartResearchCommandHandler> _logger;

        public StartResearchCommandHandler(
            IVillageRepository villages,
            IPlayerResearchRepository research,
            IServerRepository servers,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            PersonalTechnologies technologies,
            PlayerTechnologies bonuses,
            VillageStatus status,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<StartResearchCommandHandler> logger)
        {
            _villages = villages;
            _research = research;
            _servers = servers;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _technologies = technologies;
            _bonuses = bonuses;
            _status = status;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(StartResearchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villages.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var laboratory = _technologies.BuildingKey is { } key && _catalog.Buildings.TryGetValue(key, out var config)
                ? config
                : throw new EntityNotFoundException("Technology building", _technologies.BuildingKey ?? "none");

            if (!_status.IsUnlocked(village, laboratory.Key))
                throw new RequirementNotMetException(RefusalReasons.ResearchLaboratoryLocked,
                    $"The laboratory opens at town hall {laboratory.RequiresMainBuildingLevel}.", laboratory.RequiresMainBuildingLevel);

            var node = _technologies.Tree.Node(request.Node)
                ?? throw new EntityNotFoundException("Technology", request.Node);
            var tier = _technologies.Tree.Tier(request.Tier)
                ?? throw new EntityNotFoundException("Technology tier", request.Tier.ToString());

            var research = await _research.GetByPlayerAsync(request.PlayerId, cancellationToken);
            if (research is null)
            {
                research = new PlayerResearch(Guid.NewGuid(), request.PlayerId, _serverContext.ServerId, now);
                await _research.AddAsync(research, cancellationToken);
            }

            // Дозріле дослідження звільняє Лабораторію саме, без чекання сканера
            research.CompleteDue(now);

            var server = await _servers.GetByIdAsync(_serverContext.ServerId, cancellationToken)
                ?? throw new InvalidOperationException($"Server {_serverContext.ServerId} not found.");

            EnsureAvailable(research, request.Tier, node, tier, _status.MainBuildingLevel(village), server.Level);

            var technologyKey = new TechnologyKey(request.Tier, node.Key);
            var level = research.LevelOf(technologyKey) + 1;

            village.ChargeCost(_technologies.Cost(request.Tier, node, level), now);

            // Швидкість досліджень ділить тривалість, а не зрізає частку: до нуля вона не доходить
            var bonuses = await _bonuses.ForPlayerAsync(request.PlayerId, cancellationToken);
            var duration = _technologies.Duration(request.Tier, node, level) / (1 + bonuses.Of(TechnologyStat.ResearchSpeed));

            research.Start(technologyKey, duration, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} started researching {Key} level {Level} for {Duration}",
                request.PlayerId, technologyKey, level, duration);
        }

        private void EnsureAvailable(PlayerResearch research, int tier, TechnologyNodeConfig node, TechnologyTierConfig tierConfig,
            int mainBuildingLevel, int worldLevel)
        {
            // Зайнята Лабораторія — перша причина: решта не важить, доки дослідження йде
            if (research.IsResearching)
                throw new RequirementNotMetException(RefusalReasons.ResearchBusy,
                    $"Player {research.PlayerId} is already researching {research.ActiveKey}.");

            switch (_technologies.Tree.Availability(research.Levels, tier, node.Key, mainBuildingLevel, worldLevel))
            {
                case TechnologyAvailability.Maxed:
                    throw new RequirementNotMetException(RefusalReasons.ResearchMaxed, $"{node.Key} is maxed on tier {tier}.");

                case TechnologyAvailability.TierGated:
                    throw new RequirementNotMetException(RefusalReasons.ResearchTierLocked,
                        $"Tier {tier} needs town hall {tierConfig.MainBuildingLevel} and world level {tierConfig.WorldLevel}.",
                        tier, tierConfig.MainBuildingLevel, tierConfig.WorldLevel);

                case TechnologyAvailability.PreviousTierIncomplete:
                    throw new RequirementNotMetException(RefusalReasons.ResearchPreviousTier,
                        $"Tier {tier - 1} of {node.Tab} is not finished.", tier - 1);

                case TechnologyAvailability.PrerequisitesMissing:
                    var missing = node.Requires.Where(r => TechnologyTree.LevelOf(research.Levels, tier, r) < 1);
                    throw new RequirementNotMetException(RefusalReasons.ResearchPrerequisites,
                        $"{node.Key} needs a level of its prerequisites first.", string.Join(",", missing));

                case TechnologyAvailability.UnknownNode:
                    throw new EntityNotFoundException("Technology", node.Key);
            }
        }
    }
}
