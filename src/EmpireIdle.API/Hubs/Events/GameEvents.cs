namespace EmpireIdle.API.Hubs.Events
{
    /// <param name="NewVillageAmount">Баланс після зарахування: клієнту не треба перезапитувати село.</param>
    public record BuildingCollectedEvent(Guid BuildingId, string ResourceType, int Collected, int NewVillageAmount);

    /// <param name="CompletesAt">UTC. Клієнт рахує таймер від нього, а не від власного годинника.</param>
    public record UpgradeStartedEvent(Guid BuildingId, DateTime CompletesAt);

    public record UpgradeCompletedEvent(Guid BuildingId, int NewLevel);

    /// <param name="ReportId">Повний звіт тягнеться окремим запитом: у подію він не влазить.</param>
    public record BattleFinishedEvent(Guid ReportId, bool Won, string TargetName);

    /// <summary>Армія вдома: юніти в гарнізоні, здобич на складі. Клієнт перечитує гарнізон, село й марші.</summary>
    public record MarchReturnedEvent(Guid MarchId);

    public record ServerQuestRewardedEvent(string QuestKey, int Rank, long Contribution);

    public record ClanInviteEvent(Guid RequestId, Guid ClanId, string ClanName, string ClanTag, DateTime ExpiresAt);
}
