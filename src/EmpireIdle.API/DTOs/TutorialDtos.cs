namespace EmpireIdle.API.DTOs;

/// <summary>Прогрес навчання: побачені кроки й момент відмови від ведення (null — ще веде).</summary>
public record TutorialProgressResponse(List<string> SeenSteps, DateTime? SkippedAt);
