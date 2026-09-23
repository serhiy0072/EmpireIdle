namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>Активне замовлення на підняття рівня в залі героїв.</summary>
    /// <param name="SpeedUpCostGems">Ціна миттєвого завершення зараз — клієнт показує її до кліку.</param>
    public record HeroLevelOrderSummary(Guid Id, Guid HeroId, int TargetLevel, DateTime CompletesAt, int SpeedUpCostGems);
}
