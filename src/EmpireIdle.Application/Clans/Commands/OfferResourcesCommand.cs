using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Підношення ресурсом у вибрану технологію (рішення 12.11): витрачає спробу й ресурс села.
    /// Ресурс і ціну задає сама технологія та її тір, ратуша гравця на ціну не впливає.
    /// </summary>
    public record OfferResourcesCommand(Guid PlayerId, int Tier, string Node)
        : IRequest<ClanOfferingResult>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class OfferResourcesCommandHandler : IRequestHandler<OfferResourcesCommand, ClanOfferingResult>
    {
        private readonly ClanOfferings _offerings;
        private readonly IVillageRepository _villages;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanTechnologies _technologies;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OfferResourcesCommandHandler> _logger;

        public OfferResourcesCommandHandler(ClanOfferings offerings, IVillageRepository villages, IUnitOfWork unitOfWork,
            ClanTechnologies technologies, TimeProvider timeProvider, ILogger<OfferResourcesCommandHandler> logger)
        {
            _offerings = offerings;
            _villages = villages;
            _unitOfWork = unitOfWork;
            _technologies = technologies;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ClanOfferingResult> Handle(OfferResourcesCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _offerings.ClanOfAsync(request.PlayerId, cancellationToken);
            var research = await _offerings.ResearchOfAsync(clan, now, cancellationToken);

            var key = new TechnologyKey(request.Tier, request.Node);
            var node = await _offerings.AvailableNodeAsync(research, key, cancellationToken);
            var cost = _technologies.AttemptCost(request.Tier, node)
                ?? throw new InvalidOperationException($"Clan technology {key} has no priced resource.");

            var village = await _villages.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var ledger = await _offerings.LedgerOfAsync(request.PlayerId, now, cancellationToken);
            var offering = _technologies.Offering;

            ledger.SpendAttempt(_technologies.MaxAttempts(research.Levels), TimeSpan.FromSeconds(offering.AttemptRechargeSeconds), now);
            village.ChargeCost([cost], now);

            var result = await _offerings.RecordAsync(clan, research, ledger, request.PlayerId, key, node,
                offering.PointsPerAttempt, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} offered {Cost} {Resource} to {Technology} of clan {ClanId}: {Coins} coins",
                request.PlayerId, cost.Amount, cost.Resource, key, clan.Id, result.Coins);

            return result;
        }
    }
}
