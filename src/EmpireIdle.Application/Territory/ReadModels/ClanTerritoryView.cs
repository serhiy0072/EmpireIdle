namespace EmpireIdle.Application.Territory.ReadModels
{
    /// <summary>
    /// Територія клану очима учасника: очки вкладу, слоти й умови їх відкриття,
    /// споруди з гарнізонами й квести клану.
    /// </summary>
    /// <param name="Enabled">Чи є територія в цьому світі; ні — екран показує лише квести й очки.</param>
    /// <param name="CanBuild">Чи роль гравця дозволяє закладати й зносити споруди.</param>
    public record ClanTerritoryView(
        bool Enabled,
        long Points,
        long StructureCost,
        int Radius,
        int BuildMinutes,
        int GarrisonCapacity,
        int SlotsOpen,
        int SlotsMax,
        bool CanBuild,
        List<ClanSlotUnlockView> SlotUnlocks,
        List<ClanStructureView> Structures,
        List<ClanQuestView> Quests,
        List<ClanContributorView> Contributors);

    /// <summary>Умова одного слота: поріг учасників або квест клану.</summary>
    public record ClanSlotUnlockView(int? MinMembers, string? QuestKey, bool Unlocked);

    /// <param name="ReadyAt">UTC: до цього моменту споруда будується й бонусу не дає.</param>
    /// <param name="MyUnits">Скільки юнітів гравця стоїть у гарнізоні — щоб знати, що відкликати.</param>
    public record ClanStructureView(Guid Id, int X, int Y, DateTime ReadyAt, double AcceleratedShare,
        int GarrisonUnits, int MyUnits);

    public record ClanQuestView(string Key, string DisplayName, string ObjectiveType, string? ObjectiveTarget,
        long Amount, long Target, bool Completed, long ClanPoints, bool OpensSlot);

    public record ClanContributorView(Guid PlayerId, string Name, long Contribution);
}
