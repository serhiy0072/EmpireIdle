namespace EmpireIdle.API.DTOs;

/// <summary>Наступний рівень вузла особистих технологій на тірі.</summary>
public record StartResearchRequest(int Tier, string Node);
