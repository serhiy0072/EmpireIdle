namespace EmpireIdle.Application.Heroes.Contracts
{
    /// <summary>
    /// Усе, що потрібно екрану героїв: ростер, уламки, пул досвіду, кап маршів і навчальний табір.
    ///
    /// MarchCapacity тут, бо окремого лічильника слотів немає — один герой
    /// веде один марш, і межа рахується з ростера та конфіга.
    /// </summary>
    public record HeroesOverview(
        List<HeroSummary> Heroes,
        List<HeroShardSummary> Shards,
        long Experience,
        int MarchCapacity,
        TrainingCampView Camp);

    /// <summary>Навчальний табір (GDD §6.1, рішення 07.10.2026).</summary>
    /// <param name="Available">Поза табором щонайменше ReferenceSize героїв — опорна п'ятірка є.</param>
    /// <param name="Level">Рівень, який дає табір: найменший у п'ятірці; null — табір недоступний.</param>
    /// <param name="ReferenceHeroIds">Опорна п'ятірка — найсильніші поза табором, від найсильнішого; може бути коротшою, якщо героїв мало.</param>
    /// <param name="TotalSlots">Відкриті слоти: безкоштовні за ратушею плюс куплені.</param>
    /// <param name="NextSlotPriceGems">Ціна наступного слота за gems; null — усі куплені.</param>
    /// <param name="Slots">Кожен відкритий слот: хто в ньому й до коли перезаряджається.</param>
    public record TrainingCampView(
        bool Available,
        int? Level,
        int ReferenceSize,
        IReadOnlyList<Guid> ReferenceHeroIds,
        int TotalSlots,
        int FreeSlots,
        int PurchasedSlots,
        int MaxPurchasableSlots,
        int? NextSlotPriceGems,
        int SkipCooldownGems,
        IReadOnlyList<CampSlotView> Slots);

    /// <param name="Index">Номер слота від 0.</param>
    /// <param name="HeroId">Герой у слоті; null — вільний.</param>
    /// <param name="CooldownUntil">До коли слот перезаряджається після звільнення; null — готовий.</param>
    public record CampSlotView(int Index, Guid? HeroId, DateTime? CooldownUntil);
}
