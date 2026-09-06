using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>Активний запит на кланову допомогу.</summary>
    public record ClanHelpItem(
        Guid RequestId,
        Guid PlayerId,
        string PlayerName,
        ClanHelpTarget TargetType,
        Guid TargetId,
        int HelpCount,
        int MaxHelpers,
        bool AlreadyHelped,
        bool IsMine,
        DateTime CreatedAt,
        DateTime ExpiresAt);
}
