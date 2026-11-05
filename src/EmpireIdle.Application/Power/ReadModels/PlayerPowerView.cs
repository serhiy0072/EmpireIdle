namespace EmpireIdle.Application.Power.ReadModels
{
    /// <summary>Бойова сила гравця з розкладкою по джерелах.</summary>
    public record PlayerPowerView(
        double Total,
        double Army,
        double Hero,
        double Equipment,
        double Technology,
        double ClanTechnology,
        DateTime UpdatedAt);
}
