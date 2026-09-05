using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Картка чужого клану — те, що видно до вступу.</summary>
    public record ClanProfile(
        Guid Id,
        string Name,
        string Tag,
        string Description,
        ClanJoinPolicy JoinPolicy,
        int MemberCount,
        int Capacity,
        bool IsFull,
        DateTime CreatedAt);

    /// <summary>Клан за id. Склад назовні не віддається — його бачать лише свої.</summary>
    public record GetClanProfileQuery(Guid ClanId) : IRequest<ClanProfile>;

    /// <summary>Обробник GetClanProfileQuery.</summary>
    public sealed class GetClanProfileQueryHandler : IRequestHandler<GetClanProfileQuery, ClanProfile>
    {
        private readonly IClanRepository _clanRepository;
        private readonly GameCatalog _catalog;

        public GetClanProfileQueryHandler(IClanRepository clanRepository, GameCatalog catalog)
        {
            _clanRepository = clanRepository;
            _catalog = catalog;
        }

        public async Task<ClanProfile> Handle(GetClanProfileQuery request, CancellationToken cancellationToken)
        {
            var card = await _clanRepository.GetCardAsync(request.ClanId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan", request.ClanId);

            var capacity = _catalog.Config.Clan.Capacity;

            return new ClanProfile(card.Id, card.Name, card.Tag, card.Description, card.JoinPolicy,
                card.MemberCount, capacity, card.MemberCount >= capacity, card.CreatedAt);
        }
    }
}
