using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>Активний запит на кланову допомогу.</summary>
    /// <param name="Subject">
    /// Що саме прискорюють: для дослідження — ключ технології «тір:вузол». Для будівництва
    /// й тренування поки null — клієнт показує загальний підпис.
    /// </param>
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
        DateTime ExpiresAt,
        string? Subject);
}
