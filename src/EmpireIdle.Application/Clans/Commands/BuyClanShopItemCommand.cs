using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Купівля в клановому магазині за монети (рішення 03.11). Магазин відкритий лише в клані, але монети
    /// й ліміти лежать в особистій книзі, тож перехід у інший клан їх не змінює. Клієнт шле ціну, яку показав:
    /// опівночі вітрина змінюється, і знижка, на яку гравець натиснув, могла вже зникнути.
    /// </summary>
    public record BuyClanShopItemCommand(Guid PlayerId, ClanShopKind Kind, string ItemKey, int Quantity, int ExpectedPrice)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class BuyClanShopItemCommandHandler : IRequestHandler<BuyClanShopItemCommand>
    {
        private readonly IClanRepository _clans;
        private readonly IClanOfferingRepository _offerings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanShop _shop;
        private readonly RewardDispatcher _rewards;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BuyClanShopItemCommandHandler> _logger;

        public BuyClanShopItemCommandHandler(IClanRepository clans, IClanOfferingRepository offerings, IUnitOfWork unitOfWork,
            ClanShop shop, RewardDispatcher rewards, TimeProvider timeProvider, ILogger<BuyClanShopItemCommandHandler> logger)
        {
            _clans = clans;
            _offerings = offerings;
            _unitOfWork = unitOfWork;
            _shop = shop;
            _rewards = rewards;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(BuyClanShopItemCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (await _clans.GetClanIdByMemberAsync(request.PlayerId, cancellationToken) is null)
                throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

            var offer = _shop.Offers(request.Kind, now).FirstOrDefault(o => o.ItemKey == request.ItemKey)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanTechShopItemGone,
                    $"{request.ItemKey} is not on the {request.Kind} showcase now.");

            if (offer.Price != request.ExpectedPrice)
                throw new RequirementNotMetException(RefusalReasons.ClanTechShopPriceChanged,
                    $"{request.ItemKey} costs {offer.Price}, the client expected {request.ExpectedPrice}.", offer.Price);

            var total = (long)offer.Price * request.Quantity;

            // Книги ще немає — гравець не зробив жодного підношення, тож і монет у нього нема
            var ledger = await _offerings.GetLedgerAsync(request.PlayerId, cancellationToken)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanTechNoCoins, "The player has no clan coins.", total, 0L);

            ledger.Purchase(ClanShop.Period(request.Kind, now), offer.ItemKey, request.Quantity, offer.Limit, total, ClanShop.LivePeriods(now));

            await _rewards.GrantAllAsync(request.PlayerId,
                [new RewardConfig { Type = "Item", Key = offer.ItemKey, Amount = request.Quantity }],
                $"clan-shop:{ClanShop.Period(request.Kind, now)}", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} bought {Quantity} {Item} from the {Kind} clan shop for {Total} coins",
                request.PlayerId, request.Quantity, offer.ItemKey, request.Kind, total);
        }
    }
}
