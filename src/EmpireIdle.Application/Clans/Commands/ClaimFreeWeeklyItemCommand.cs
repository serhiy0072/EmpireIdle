using EmpireIdle.Application.Clans.Services;
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
    /// Безкоштовний предмет тижневої вітрини (рішення 05.11). Окремо від купівлі за монети: тут немає ні ціни,
    /// ні знижок, а клан потрібен лише предметам із RequiresClan. Ліміт лежить в особистій книзі, тому вихід
    /// із клану чи перехід в інший його не поновлює.
    /// </summary>
    public record ClaimFreeWeeklyItemCommand(Guid PlayerId, string ItemKey, int Quantity)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class ClaimFreeWeeklyItemCommandHandler : IRequestHandler<ClaimFreeWeeklyItemCommand>
    {
        private readonly IClanRepository _clans;
        private readonly ClanOfferings _offerings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ClanShop _shop;
        private readonly RewardDispatcher _rewards;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ClaimFreeWeeklyItemCommandHandler> _logger;

        public ClaimFreeWeeklyItemCommandHandler(IClanRepository clans, ClanOfferings offerings, IUnitOfWork unitOfWork,
            ClanShop shop, RewardDispatcher rewards, TimeProvider timeProvider, ILogger<ClaimFreeWeeklyItemCommandHandler> logger)
        {
            _clans = clans;
            _offerings = offerings;
            _unitOfWork = unitOfWork;
            _shop = shop;
            _rewards = rewards;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(ClaimFreeWeeklyItemCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var free = _shop.FreeWeekly.FirstOrDefault(f => f.ItemKey == request.ItemKey)
                ?? throw new RequirementNotMetException(RefusalReasons.ClanTechShopItemGone,
                    $"{request.ItemKey} is not on the free weekly showcase.");

            if (free.RequiresClan && await _clans.GetClanIdByMemberAsync(request.PlayerId, cancellationToken) is null)
                throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

            var ledger = await _offerings.LedgerOfAsync(request.PlayerId, now, cancellationToken);
            var period = ClanShop.FreePeriod(now);

            ledger.Purchase(period, free.ItemKey, request.Quantity, free.Limit, totalPrice: 0, ClanShop.LivePeriods(now));

            await _rewards.GrantAllAsync(request.PlayerId,
                [new RewardConfig { Type = "Item", Key = free.ItemKey, Amount = request.Quantity }],
                $"clan-free:{period}", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} claimed {Quantity} free {Item}", request.PlayerId, request.Quantity, free.ItemKey);
        }
    }
}
