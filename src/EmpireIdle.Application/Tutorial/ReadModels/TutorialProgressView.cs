namespace EmpireIdle.Application.Tutorial.ReadModels
{
    /// <summary>Прогрес навчання очима клієнта: що вже показано і чи гравець відмовився від ведення.</summary>
    public record TutorialProgressView(IReadOnlyList<string> SeenSteps, DateTime? SkippedAt);
}
