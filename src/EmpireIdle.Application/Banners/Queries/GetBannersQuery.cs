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
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly TimeProvider _timeProvider;
        private readonly LocaleTexts _texts;

        public GetBannersQueryHandler(GameCatalog catalog, BannerRoller roller, IBannerRepository banners,
            IServerRepository serverRepository, IServerContext serverContext, TimeProvider timeProvider, LocaleTexts texts)
        {
            _texts = texts;
            _catalog = catalog;
            _roller = roller;
            _banners = banners;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<BannerView>> Handle(GetBannersQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();
            var server = await _serverRepository.GetByIdAsync(_serverContext.ServerId, cancellationToken)
                ?? throw new InvalidOperationException($"Server {_serverContext.ServerId} not found.");

            // Банер героїв вищого тіру ще не відкрився — його не показуємо, як і банер поза датами чи вікном тіру
            var active = _catalog.Config.Shop.Banners
                .Where(b => server.Level >= b.RequiresServerLevel)
                .Where(b => b.StartsAt is not { } start || now >= start)
                .Where(b => b.ClosesAt(server.Level, server.LevelSince) is not { } end || now < end)
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
                    _texts.Name("banner", banner.Key, banner.DisplayName),
                    banner.Kind,
                    banner.PityGroup,
                    banner.PriceGems,
                    banner.RarePity,
                    banner.UniquePity,
                    banner.FeaturedKey,
                    banner.ClosesAt(server.Level, server.LevelSince),
                    state.RareSince,
                    state.UniqueSince,
                    state.FeaturedGuaranteed,
                    banner.Drops
                        .Select(d => new BannerDropView(d.Key, _texts.Name("bannerDrop", d.Key, d.DisplayName), d.Rarity, d.Kind, odds.GetValueOrDefault(d.Key)))
                        .ToList()));
            }

            return views;
        }
    }
}
