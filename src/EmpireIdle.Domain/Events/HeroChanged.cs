namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Ростер героя змінився так, що змінилась його сила: новий герой, рівень,
    /// тір, сузір'я. Несе гравця, а не гарнізон: герой може бути в дорозі,
    /// а сила рахується власнику.
    /// </summary>
    public record HeroChanged(Guid PlayerId, Guid HeroId, DateTime OccurredAt) : IDomainEvent;
}
