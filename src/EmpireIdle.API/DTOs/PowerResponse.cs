namespace EmpireIdle.API.DTOs
{
    /// <summary>Бойова сила гравця з розкладкою по джерелах.</summary>
    public record PowerResponse(
        double Total,
        double Army,
        double Hero,
        double Equipment,
        DateTime UpdatedAt);
}
