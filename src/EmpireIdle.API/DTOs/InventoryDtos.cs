namespace EmpireIdle.API.DTOs;

/// <summary>Інвентар гравця.</summary>
public record InventoryResponse(
    List<InventoryItemResponse> Items,
    List<EquipmentResponse> Equipment,
    List<ActiveEffectResponse> ActiveEffects);

/// <summary>Стаковий предмет із описом із конфіга.</summary>
public record InventoryItemResponse(
    string ItemKey, string DisplayName, string Description,
    string Rarity, string Type, int Count);

/// <summary>Екземпляр спорядження.</summary>
/// <param name="SlotIndex">Номер слота на герої: зброя завжди 0, артефакт — позиція його типу в каталозі (ArtifactSlots).</param>
/// <param name="IsBroken">Зламане заточкою: не одягається, поки не відремонтоване.</param>
/// <param name="IsOnMarket">Виставлене на ринок: у заставі, не одягається й не заточується.</param>
/// <param name="ResaleLockedUntil">Куплене на ринку не перепродається до цього моменту; null — обмеження немає.</param>
public record EquipmentResponse(
    Guid Id, string ItemKey, string Slot, string Rarity,
    int EnhancementLevel, Guid? EquippedByHeroId, int SlotIndex, bool IsBroken,
    Dictionary<string, double> Stats, bool IsOnMarket, DateTime? ResaleLockedUntil);

/// <summary>Діючий буст.</summary>
public record ActiveEffectResponse(string Target, double Multiplier, DateTime ExpiresAt, string SourceItemKey);

/// <summary>
/// Запит на використання предмета.
/// TargetId — для предметів, що діють на сутність; TargetX/TargetY — на клітину карти.
/// </summary>
public record UseItemRequest(string ItemKey, int Count, Guid? TargetId = null, int? TargetX = null, int? TargetY = null);

/// <summary>Результат спроби заточки: success, failure або broken.</summary>
public record EnhancementResponse(string Outcome);
