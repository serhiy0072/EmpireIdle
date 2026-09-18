using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Banners.Queries
{
    /// <summary>Вітрина банерів: пул, шанси й поточний прогрес pity гравця.</summary>
    public record GetBannersQuery(Guid PlayerId) : IRequest<IReadOnlyList<BannerView>>, IPlayerScopedRequest;

    /// <param name="RareSince">Скільки роллів гравець уже зробив без рідкісного в цій групі.</param>
    public record BannerView(
        string Key,
        string DisplayName,
        BannerKind Kind,
        string PityGroup,
        int PriceGems,
        int RarePity,
        int UniquePity,
        string? FeaturedKey,
        DateTimeOffset? EndsAt,
        int RareSince,
        int UniqueSince,
        bool FeaturedGuaranteed,
        IReadOnlyList<BannerDropView> Drops);

    /// <param name="Chance">Базовий шанс у відсотках, без урахування гарантій.</param>
    public record BannerDropView(string Key, string DisplayName, Rarity Rarity, BannerKind? Kind, double Chance);

    internal sealed class GetBannersQueryHandler : IRequestHandler<GetBannersQuery, IReadOnlyList<BannerView>>
    {
        private readonly GameCatalog _catalog;
        private readonly BannerRoller _roller;
        private readonly IBannerRepository _banners;
        private readonly TimeProvider _timeProvider;

        public GetBannersQueryHandler(GameCatalog catalog, BannerRoller roller, IBannerRepository banners, TimeProvider timeProvider)
        {
            _catalog = catalog;
            _roller = roller;
            _banners = banners;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<BannerView>> Handle(GetBannersQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();

            var active = _catalog.Config.Shop.Banners
                .Where(b => (b.StartsAt is not { } start || now >= start) && (b.EndsAt is not { } end || now < end))
                .ToList();

            // Прогрес спільний для групи, тож читаємо по групі, а не по банеру
            var progress = new Dictionary<string, BannerPityProgress?>();

            foreach (var group in active.Select(b => b.PityGroup).Distinct())
                progress[group] = await _banners.GetPityAsync(request.PlayerId, group, cancellationToken);

            var views = new List<BannerView>(active.Count);

            foreach (var banner in active)
            {
                var odds = _roller.Odds(banner);
                var state = progress[banner.PityGroup]?.State ?? PityState.Empty;

                views.Add(new BannerView(
                    banner.Key,
                    banner.DisplayName,
                    banner.Kind,
                    banner.PityGroup,
                    banner.PriceGems,
                    banner.RarePity,
                    banner.UniquePity,
                    banner.FeaturedKey,
                    banner.EndsAt,
                    state.RareSince,
                    state.UniqueSince,
                    state.FeaturedGuaranteed,
                    banner.Drops
                        .Select(d => new BannerDropView(d.Key, d.DisplayName, d.Rarity, d.Kind, odds.GetValueOrDefault(d.Key)))
                        .ToList()));
            }

            return views;
        }
    }
}
