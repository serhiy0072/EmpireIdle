using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Рядок списку кланів.</summary>
    public record ClanListItem(
        Guid Id,
        string Name,
        string Tag,
        string Description,
        ClanJoinPolicy JoinPolicy,
        int MemberCount,
        int Capacity,
        bool IsFull);

    /// <summary>Сторінка списку із загальною кількістю збігів.</summary>
    public record ClanListPage(List<ClanListItem> Items, int Total, int Page, int PageSize);

    /// <summary>
    /// Клани світу для вступу. Не player-scoped: клани публічні в межах
    /// світу, а світ уже прийшов із токена.
    /// </summary>
    public record BrowseClansQuery(string? Search = null, int Page = 1, int PageSize = 20)
        : IRequest<ClanListPage>;

    /// <summary>Обробник BrowseClansQuery.</summary>
    public sealed class BrowseClansQueryHandler : IRequestHandler<BrowseClansQuery, ClanListPage>
    {
        private const int MaxPageSize = 50;

        private readonly IClanRepository _clanRepository;
        private readonly GameCatalog _catalog;

        public BrowseClansQueryHandler(IClanRepository clanRepository, GameCatalog catalog)
        {
            _clanRepository = clanRepository;
            _catalog = catalog;
        }

        public async Task<ClanListPage> Handle(BrowseClansQuery request, CancellationToken cancellationToken)
        {
            var page = Math.Max(request.Page, 1);
            var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
            var capacity = _catalog.Config.Clan.Capacity;

            var (cards, total) = await _clanRepository.BrowseAsync(
                request.Search, (page - 1) * pageSize, pageSize, cancellationToken);

            var items = cards
                .Select(c => new ClanListItem(c.Id, c.Name, c.Tag, c.Description, c.JoinPolicy,
                    c.MemberCount, capacity, c.MemberCount >= capacity))
                .ToList();

            return new ClanListPage(items, total, page, pageSize);
        }
    }
}
