using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Учасник у складі клану.</summary>
    public record ClanMemberView(
        Guid PlayerId,
        string PlayerName,
        Guid RoleId,
        string RoleName,
        int Rank,
        double Power,
        DateTime JoinedAt,
        DateTime LastActiveAt);

    /// <summary>Роль клану — для екрана керування складом.</summary>
    public record ClanRoleView(
        Guid Id,
        string Name,
        int Rank,
        ClanPermission Permissions,
        bool IsLeaderRole,
        bool IsDefaultRole);

    /// <summary>Клан очима його учасника — зі складом, ролями і власними дозволами.</summary>
    public record MyClan(
        Guid Id,
        string Name,
        string Tag,
        string Description,
        ClanJoinPolicy JoinPolicy,
        int MemberCount,
        int Capacity,
        DateTime CreatedAt,
        Guid MyRoleId,
        ClanPermission MyPermissions,
        List<ClanRoleView> Roles,
        List<ClanMemberView> Members);

    /// <summary>
    /// Клан гравця; null — гравець без клану. Це нормальний стан екрана,
    /// а не помилка, тож 404 тут був би брехнею.
    /// </summary>
    public record GetMyClanQuery(Guid PlayerId) : IRequest<MyClan?>, IPlayerScopedRequest;

    /// <summary>Обробник GetMyClanQuery.</summary>
    public sealed class GetMyClanQueryHandler : IRequestHandler<GetMyClanQuery, MyClan?>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerPowerRepository _powerRepository;
        private readonly GameCatalog _catalog;

        public GetMyClanQueryHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IPlayerPowerRepository powerRepository,
            GameCatalog catalog)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _powerRepository = powerRepository;
            _catalog = catalog;
        }

        public async Task<MyClan?> Handle(GetMyClanQuery request, CancellationToken cancellationToken)
        {
            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken);

            if (clan is null)
                return null;

            var myRole = clan.RoleOf(request.PlayerId)
                ?? throw new EntityNotFoundException("Clan member", request.PlayerId);

            var playerIds = clan.Members.Select(m => m.PlayerId).ToList();

            // Імена й сила одним запитом на весь склад: інакше двісті
            // учасників дали б чотириста звернень до бази
            var names = await _playerRepository.GetNamesAsync(playerIds, cancellationToken);
            var powerByPlayer = await _powerRepository.GetTotalPowerAsync(playerIds, cancellationToken);

            var rolesById = clan.Roles.ToDictionary(r => r.Id);

            var members = clan.Members
                .Select(m =>
                {
                    var role = rolesById[m.RoleId];

                    return new ClanMemberView(
                        m.PlayerId,
                        names.GetValueOrDefault(m.PlayerId, "—"),
                        role.Id,
                        role.Name,
                        role.Rank,
                        powerByPlayer.GetValueOrDefault(m.PlayerId),
                        m.JoinedAt,
                        m.LastActiveAt);
                })
                .OrderByDescending(m => m.Rank)
                .ThenByDescending(m => m.Power)
                .ToList();

            var roles = clan.Roles
                .OrderByDescending(r => r.Rank)
                .Select(r => new ClanRoleView(r.Id, r.Name, r.Rank, r.Permissions, r.IsLeaderRole, r.IsDefaultRole))
                .ToList();

            return new MyClan(
                clan.Id, clan.Name, clan.Tag, clan.Description, clan.JoinPolicy,
                clan.Members.Count, _catalog.Config.Clan.Capacity, clan.CreatedAt,
                myRole.Id, myRole.Permissions, roles, members);
        }
    }
}
