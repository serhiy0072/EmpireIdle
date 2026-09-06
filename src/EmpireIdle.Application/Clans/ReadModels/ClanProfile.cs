using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Clans.ReadModels
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
}
