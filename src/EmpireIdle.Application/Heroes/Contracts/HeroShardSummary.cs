namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>Прогрес уламків: скільки зібрано з потрібного на призов.</summary>
    public record HeroShardSummary(string HeroKey, int Count, int Required);
}
