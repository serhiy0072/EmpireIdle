using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Clans.ReadModels
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
}
