namespace EmpireIdle.API.DTOs;

/// <summary>
/// Територія клану гравця (GDD §7.2): очки вкладу, слоти, споруди й квести клану.
/// Enabled=false — світ без території: екран показує лише очки й квести.
/// </summary>
/// <param name="BuildMinutes">Скільки будується споруда без допомоги маршів.</param>
/// <param name="CanBuild">Чи роль гравця дозволяє закладати й зносити споруди.</param>
public record ClanTerritoryResponse(
    bool Enabled,
    long Points,
    long StructureCost,
    int Radius,
    int BuildMinutes,
    int GarrisonCapacity,
    int SlotsOpen,
    int SlotsMax,
    bool CanBuild,
    List<ClanSlotUnlockResponse> SlotUnlocks,
    List<ClanStructureResponse> Structures,
    List<ClanQuestResponse> Quests,
    List<ClanContributorResponse> Contributors);

/// <summary>Умова одного слота: рівно одне з MinMembers чи QuestKey.</summary>
public record ClanSlotUnlockResponse(int? MinMembers, string? QuestKey, bool Unlocked);

/// <param name="ReadyAt">UTC: до цього моменту споруда будується й бонусу не дає.</param>
/// <param name="AcceleratedShare">Яку частку будівництва вже зрізали марші (0..1).</param>
/// <param name="MyUnits">Юніти гравця в гарнізоні споруди.</param>
public record ClanStructureResponse(Guid Id, int X, int Y, DateTime ReadyAt, double AcceleratedShare,
    int GarrisonUnits, int MyUnits);

/// <param name="OpensSlot">Чи завершення цього квесту відкриває слот під споруду.</param>
public record ClanQuestResponse(string Key, string DisplayName, string ObjectiveType, string? ObjectiveTarget,
    long Amount, long Target, bool Completed, long ClanPoints, bool OpensSlot);

public record ClanContributorResponse(Guid PlayerId, string Name, long Contribution);

/// <summary>Клітина, на якій клан закладає споруду.</summary>
public record PlaceClanStructureRequest(int X, int Y);
