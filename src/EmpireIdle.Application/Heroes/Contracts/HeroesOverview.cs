namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>
    /// Усе, що потрібно екрану героїв: ростер, уламки, черга й кап маршів.
    ///
    /// MarchCapacity тут, бо окремого лічильника слотів немає — один герой
    /// веде один марш, і межа рахується з ростера та конфіга.
    /// </summary>
    public record HeroesOverview(
        List<HeroSummary> Heroes,
        List<HeroShardSummary> Shards,
        HeroLevelOrderSummary? ActiveOrder,
        int MarchCapacity);
}
