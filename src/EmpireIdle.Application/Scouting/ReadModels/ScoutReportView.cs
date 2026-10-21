using EmpireIdle.Domain.Entities;
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
        Dictionary<string, long> Lootable,
        DateTime CreatedAt)
    {
        /// <summary>Одна проєкція на список звітів і на лист скриньки.</summary>
        public static ScoutReportView From(ScoutReport report)
            => new(report.Id, report.TargetType, report.TargetId, report.TargetName, report.X, report.Y,
                report.Outcome.ToString(), report.DefencePower,
                report.Resources.ToDictionary(x => x.ResourceType, x => x.Amount), report.CreatedAt);
    }
}
