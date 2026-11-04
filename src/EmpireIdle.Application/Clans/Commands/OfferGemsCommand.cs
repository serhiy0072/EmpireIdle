using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Підношення за gems (рішення 03.11): ціна подвоюється кожні п'ять підношень за добу UTC.
    /// Клієнт шле ціну, яку показав: остання за день коштує тисячі gems, і друга вкладка не має
    /// мовчки взяти більше, ніж гравець бачив.
    /// </summary>
    public record OfferGemsCommand(Guid PlayerId, int ExpectedPrice)
        : IRequest<ClanOfferingResult>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class OfferGemsCommandHandler : IRequestHandler<OfferGemsCommand, ClanOfferingResult>
    {
        private readonly ClanOfferings _offerings;
        private readonly IPlayerWalletRepository _wallets;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanTechnologies _technologies;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<OfferGemsCommandHandler> _logger;

        public OfferGemsCommandHandler(ClanOfferings offerings, IPlayerWalletRepository wallets, ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork, ClanTechnologies technologies, TimeProvider timeProvider, ILogger<OfferGemsCommandHandler> logger)
        {
            _offerings = offerings;
            _wallets = wallets;
            _currentPlayer = currentPlayer;
            _unitOfWork = unitOfWork;
            _technologies = technologies;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ClanOfferingResult> Handle(OfferGemsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clan = await _offerings.ClanOfAsync(request.PlayerId, cancellationToken);
            var research = await _offerings.ResearchOfAsync(clan, now, cancellationToken);
            var ledger = await _offerings.LedgerOfAsync(request.PlayerId, now, cancellationToken);

            var nth = ledger.NextGemOffering(now);
            var offering = _technologies.Offering;

            if (nth > offering.GemDailyLimit)
                throw new RequirementNotMetException(RefusalReasons.ClanTechGemLimit,
                    $"Player {request.PlayerId} reached {offering.GemDailyLimit} gem offerings today.", offering.GemDailyLimit);

            var price = _technologies.GemOfferingPrice(nth);

            if (price != request.ExpectedPrice)
                throw new RequirementNotMetException(RefusalReasons.ClanTechGemPriceChanged,
                    $"The next gem offering costs {price}, the client expected {request.ExpectedPrice}.", price);

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");
            var wallet = await _wallets.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            wallet.SpendGems(new GemAmount(price), "clan-offering", request.PlayerId, now);
            ledger.RecordGemOffering(now);

            var result = await _offerings.RecordAsync(clan, research, ledger, request.PlayerId, offering.GemPoints, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} offered {Price} gems to clan {ClanId}: {Coins} coins",
                request.PlayerId, price, clan.Id, result.Coins);

            return result;
        }
    }
}
