using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Підношення ресурсом у запас клану (рішення 28.10.2026): витрачає спробу й ресурс села.
    /// Ціна росте з ратушею, а очки однакові — внесок великого й малого села рівноцінний.
    /// Клан приймає один ресурс за раз, по черзі (рішення 05.11).
    /// </summary>
    public record OfferResourcesCommand(Guid PlayerId, string Resource)
        : IRequest<ClanOfferingResult>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class OfferResourcesCommandHandler : IRequestHandler<OfferResourcesCommand, ClanOfferingResult>
    {
        private readonly ClanOfferings _offerings;
        private readonly IVillageRepository _villages;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanTechnologies _technologies;
        private readonly VillageStatus _status;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OfferResourcesCommandHandler> _logger;

        public OfferResourcesCommandHandler(ClanOfferings offerings, IVillageRepository villages, IUnitOfWork unitOfWork,
            ClanTechnologies technologies, VillageStatus status, TimeProvider timeProvider,
            ILogger<OfferResourcesCommandHandler> logger)
        {
            _offerings = offerings;
            _villages = villages;
            _unitOfWork = unitOfWork;
            _technologies = technologies;
            _status = status;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ClanOfferingResult> Handle(OfferResourcesCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _offerings.ClanOfAsync(request.PlayerId, cancellationToken);
            var research = await _offerings.ResearchOfAsync(clan, now, cancellationToken);

            var village = await _villages.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Клієнт сам називає ресурс: якщо черга зсунулась, гравець отримає відмову, а не несподіване списання
            var accepted = request.Resource == _technologies.OfferedResource(research.ResourceOfferings);
            var cost = (accepted ? _technologies.AttemptCost(request.Resource, _status.MainBuildingLevel(village)) : null)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanTechResourceRejected,
                    $"Offerings do not accept {request.Resource}.", request.Resource);

            var ledger = await _offerings.LedgerOfAsync(request.PlayerId, now, cancellationToken);
            var offering = _technologies.Offering;

            ledger.SpendAttempt(_technologies.MaxAttempts(research.Levels), TimeSpan.FromSeconds(offering.AttemptRechargeSeconds), now);
            village.ChargeCost([new ResourceCost { Resource = request.Resource, Amount = cost }], now);
            research.CountResourceOffering();

            var result = await _offerings.RecordAsync(clan, research, ledger, request.PlayerId, offering.PointsPerAttempt, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} offered {Cost} {Resource} to clan {ClanId}: {Coins} coins",
                request.PlayerId, cost, request.Resource, clan.Id, result.Coins);

            return result;
        }
    }
}
