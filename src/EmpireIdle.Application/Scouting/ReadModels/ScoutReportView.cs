using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Scouting.ReadModels
{
    /// <summary>
    /// Звіт розвідки очима розвідника. Outcome — "Success", "Blocked", "TargetMoved" або "TargetGone";
    /// DefencePower і Lootable є лише в успішного звіту.
    /// </summary>
    /// <param name="Lootable">Що можна винести; порожньо для споруди.</param>
    public record ScoutReportView(
        Guid Id,
        MarchTargetType TargetType,
        Guid TargetId,
        string TargetName,
        int X,
        int Y,
        string Outcome,
        double? DefencePower,
        Dictionary<string, int> Lootable,
        DateTime CreatedAt);
}
