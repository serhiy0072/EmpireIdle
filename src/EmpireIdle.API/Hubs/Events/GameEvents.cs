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

    /// <summary>
    /// У скриньці нове: лист гравцеві (Kind — тип листа) або оголошення світу
    /// (Kind = "Announcement"). Клієнт перечитує скриньку й лічильник.
    /// </summary>
    public record MailReceivedEvent(string Kind);

    /// <summary>
    /// Ворожий марш іде на село чи споруду клану. Отримують захисник і весь його клан;
    /// клієнт показує банер, а на мапі веде загін від (FromX, FromY) до цілі за часом.
    /// </summary>
    /// <param name="TargetType">"Village" або "ClanStructure".</param>
    /// <param name="TargetName">Назва села або тег клану-власника споруди.</param>
    /// <param name="AttackerClanTag">null — нападник поза кланом.</param>
    public record AttackIncomingEvent(Guid MarchId, string TargetType, Guid TargetId, string? TargetName,
        int TargetX, int TargetY, int FromX, int FromY, string AttackerName, string? AttackerClanTag,
        DateTime DepartedAt, DateTime ArrivesAt);

    /// <summary>Ворожий марш розвернувся, не дійшовши: зняти тривогу й прибрати його з мапи.</summary>
    public record AttackCalledOffEvent(Guid MarchId);

    /// <summary>Кланову споруду зруйновано: слот вільний, бонус у її радіусі зник.</summary>
    public record StructureDestroyedEvent(Guid StructureId, int X, int Y);

    /// <summary>
    /// Нове повідомлення чату. Translations — переклади на мови світу:
    /// хаб не знає мови кожного отримувача, клієнт бере свою.
    /// </summary>
    /// <param name="Channel">"Server", "Clan" або "Private".</param>
    public record ChatMessageEvent(Guid Id, string Channel, Guid SenderId, string SenderName, Guid? ClanId,
        Guid? RecipientId, string Text, string Language, IReadOnlyDictionary<string, string> Translations, DateTime SentAt);
}
