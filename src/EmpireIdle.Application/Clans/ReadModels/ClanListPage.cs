using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Clans.ReadModels
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
}
