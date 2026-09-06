namespace EmpireIdle.Application.Garrisons.ReadModels
{
    /// <summary>Гарнізон у поданні для клієнта.</summary>
    public record GarrisonView(
        Guid Id,
        Guid VillageId,
        List<UnitView> Units,
        List<UnitView> Wounded,
        List<RecoverableUnitView> Recoverable,
        List<TrainingOrderView> TrainingOrders);

    /// <summary>Юніти одного типу.</summary>
    public record UnitView(string UnitType, int Count);

    /// <summary>
    /// Юніти, яких ще можна відновити за gems. Ціна залежить від каталогу,
    /// а список відфільтрований за часом — тому збирається тут, не в контролері.
    /// </summary>
    public record RecoverableUnitView(string UnitType, int Count, DateTime ExpiresAt, int CostGems);

    /// <summary>Замовлення тренування в черзі.</summary>
    public record TrainingOrderView(Guid Id, string UnitType, int Count, DateTime CompletesAt);

    /// <summary>
    /// Чужі юніти в гарнізоні. Ім'я власника обов'язкове: без нього
    /// гравець бачить купу військ і не знає, кому дякувати.
    /// </summary>
    public record ReinforcementView(Guid OwnerPlayerId, string OwnerName, string UnitType, int Count, DateTime ArrivedAt);
}
