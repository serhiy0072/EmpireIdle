using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Technologies;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Клановий магазин гравця: дві вітрини за монети й безкоштовна тижнева. Без клану запит теж працює.</summary>
    public record GetClanShopQuery(Guid PlayerId) : IRequest<ClanShopView>, IPlayerScopedRequest;

    public sealed class GetClanShopQueryHandler : IRequestHandler<GetClanShopQuery, ClanShopView>
    {
        private readonly IClanRepository _clans;
        private readonly IClanOfferingRepository _offerings;
        private readonly ClanShop _shop;
        private readonly TimeProvider _timeProvider;

        public GetClanShopQueryHandler(IClanRepository clans, IClanOfferingRepository offerings, ClanShop shop, TimeProvider timeProvider)
        {
            _clans = clans;
            _offerings = offerings;
            _shop = shop;
            _timeProvider = timeProvider;
        }

        public async Task<ClanShopView> Handle(GetClanShopQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var ledger = await _offerings.GetLedgerReadOnlyAsync(request.PlayerId, cancellationToken);
            var clanId = await _clans.GetClanIdByMemberAsync(request.PlayerId, cancellationToken);
            var freePeriod = ClanShop.FreePeriod(now);

            return new ClanShopView(
                ledger?.Coins ?? 0,
                clanId is not null,
                Showcase(ClanShopKind.Daily, ledger, now),
                Showcase(ClanShopKind.Weekly, ledger, now),
                new ClanFreeShowcaseView(
                    ClanShop.RefreshesAt(ClanShopKind.Weekly, now),
                    _shop.FreeWeekly
                        .Select(f => new ClanFreeItemView(f.ItemKey, f.Limit, ledger?.PurchasedIn(freePeriod, f.ItemKey) ?? 0, f.RequiresClan))
                        .ToList()));
        }

        private ClanShopShowcaseView Showcase(ClanShopKind kind, ClanOfferingLedger? ledger, DateTime now)
        {
            var period = ClanShop.Period(kind, now);

            return new ClanShopShowcaseView(
                ClanShop.RefreshesAt(kind, now),
                _shop.Offers(kind, now)
                    .Select(o => new ClanShopOfferView(o.ItemKey, o.BasePrice, o.Price, o.DiscountPercent, o.Limit,
                        ledger?.PurchasedIn(period, o.ItemKey) ?? 0))
                    .ToList());
        }
    }
}
