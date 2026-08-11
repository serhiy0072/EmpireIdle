namespace EmpireIdle.API.DTOs
{
    /// <summary>Скільки юнітів кожного типу викупити.</summary>
    public record RecoverUnitsRequest(Dictionary<string, int> Units);
}