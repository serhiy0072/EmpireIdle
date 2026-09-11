namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>Активне замовлення на підняття рівня в залі героїв.</summary>
    public record HeroLevelOrderSummary(Guid Id, Guid HeroId, int TargetLevel, DateTime CompletesAt);
}
