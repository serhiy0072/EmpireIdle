namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>
    /// Усе, що потрібно екрану героїв: ростер, уламки, пул досвіду й кап маршів.
    ///
    /// MarchCapacity тут, бо окремого лічильника слотів немає — один герой
    /// веде один марш, і межа рахується з ростера та конфіга.
    /// </summary>
    public record HeroesOverview(
        List<HeroSummary> Heroes,
        List<HeroShardSummary> Shards,
        long Experience,
        int MarchCapacity);
}
