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
/// <param name="SlotIndex">Номер слота на герої: позиція типу артефакта в каталозі (ArtifactSlots).</param>
/// <param name="Experience">Накопичений досвід рівня; згодований предмет передає його весь.</param>
/// <param name="ExperienceToNext">Скільки бракує до наступного рівня; null — рівень на стелі.</param>
/// <param name="FeedValue">Скільки досвіду дасть цей предмет, якщо його згодувати; null — унікальний, не годується.</param>
/// <param name="Mastery">Майстерність коваля — заточка за золото з шансом.</param>
/// <param name="IsOnMarket">Виставлене на ринок: у заставі, не одягається й не заточується.</param>
/// <param name="ResaleLockedUntil">Куплене на ринку не перепродається до цього моменту; null — обмеження немає.</param>
public record EquipmentResponse(
    Guid Id, string ItemKey, string Slot, string Rarity,
    int Level, long Experience, long? ExperienceToNext, long? FeedValue, int Mastery,
    Guid? EquippedByHeroId, int SlotIndex, Dictionary<string, double> Stats, bool IsOnMarket, DateTime? ResaleLockedUntil);

/// <summary>Згодувати артефакту спорядження й гаєчки.</summary>
/// <param name="FoodIds">Спорядження, яке зникне; унікальне не годується.</param>
/// <param name="Wrenches">Ключ гаєчки → кількість.</param>
public record FeedArtifactRequest(List<Guid> FoodIds, Dictionary<string, int> Wrenches);

/// <summary>Результат спроби майстерності: невдача з'їдає золото, але нічого не ламає.</summary>
public record ArtifactMasteryResponse(bool Success);

/// <summary>Діючий буст.</summary>
public record ActiveEffectResponse(string Target, double Multiplier, DateTime ExpiresAt, string SourceItemKey);

/// <summary>
/// Запит на використання предмета.
/// TargetX/TargetY — для предметів, що діють на клітину карти.
/// </summary>
public record UseItemRequest(string ItemKey, int Count, int? TargetX = null, int? TargetY = null);

/// <summary>Подарунок члену свого клану.</summary>
public record GiftItemRequest(Guid RecipientId, string ItemKey, int Count);

/// <summary>Прискорити таймер предметами: ключ предмета-прискорення → скільки штук.</summary>
public record SpeedUpWithItemsRequest(EmpireIdle.Domain.Enums.SpeedUpTimer Timer, Guid TargetId, Dictionary<string, int> Items);
