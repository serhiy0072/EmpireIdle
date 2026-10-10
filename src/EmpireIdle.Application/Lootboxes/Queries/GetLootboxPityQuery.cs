using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Lootboxes.Contracts;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Lootboxes.Queries
{
    /// <summary>
    /// Лічильники гарантій гравця по всіх скринях із гарантією — крамниця й рюкзак показують
    /// «гарантія через N». Скриня, яку ще не відкривали, — з нулем.
    /// </summary>
    public record GetLootboxPityQuery(Guid PlayerId) : IRequest<IReadOnlyList<LootboxPityView>>, IPlayerScopedRequest;

    internal sealed class GetLootboxPityQueryHandler : IRequestHandler<GetLootboxPityQuery, IReadOnlyList<LootboxPityView>>
    {
        private readonly ILootboxRepository _lootboxes;
        private readonly GameCatalog _catalog;

        public GetLootboxPityQueryHandler(ILootboxRepository lootboxes, GameCatalog catalog)
        {
            _lootboxes = lootboxes;
            _catalog = catalog;
        }

        public async Task<IReadOnlyList<LootboxPityView>> Handle(GetLootboxPityQuery request, CancellationToken cancellationToken)
        {
            var since = (await _lootboxes.GetPitiesReadOnlyAsync(request.PlayerId, cancellationToken))
                .ToDictionary(p => p.LootboxKey, p => p.Since);

            return _catalog.Config.Lootboxes
                .Where(box => box.PityWithin > 0)
                .Select(box => new LootboxPityView(box.Key, since.GetValueOrDefault(box.Key), box.PityWithin))
                .ToList();
        }
    }
}
