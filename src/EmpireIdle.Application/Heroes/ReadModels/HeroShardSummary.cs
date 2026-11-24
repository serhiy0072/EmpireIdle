namespace EmpireIdle.Application.Heroes.ReadModels
{
    /// <summary>Прогрес уламків: скільки зібрано з потрібного на призов.</summary>
    public record HeroShardSummary(string HeroKey, int Count, int Required);
}
