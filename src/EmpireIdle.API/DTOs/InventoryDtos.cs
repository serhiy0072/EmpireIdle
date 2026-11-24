namespace EmpireIdle.API.DTOs;

/// <summary>Згодувати артефакту спорядження й гаєчки.</summary>
/// <param name="FoodIds">Спорядження, яке зникне; унікальне не годується.</param>
/// <param name="Wrenches">Ключ гаєчки → кількість.</param>
public record FeedArtifactRequest(List<Guid> FoodIds, Dictionary<string, int> Wrenches);

/// <summary>Результат спроби заточки: невдача з'їдає золото, а на пізніх рангах може й зламати предмет.</summary>
/// <param name="Success">Заточка піднялась.</param>
/// <param name="Broken">Невдача зламала предмет — він дає половину статів до ремонту.</param>
public record ArtifactMasteryResponse(bool Success, bool Broken);

/// <summary>Ремонт зламаного артефакта.</summary>
/// <param name="UseKit">true — ремкомплектом, false — gems.</param>
public record RepairArtifactRequest(bool UseKit);

/// <summary>
/// Запит на використання предмета.
/// TargetX/TargetY — для предметів, що діють на клітину карти.
/// </summary>
public record UseItemRequest(string ItemKey, int Count, int? TargetX = null, int? TargetY = null);

/// <summary>Подарунок члену свого клану.</summary>
public record GiftItemRequest(Guid RecipientId, string ItemKey, int Count);

/// <summary>Прискорити таймер предметами: ключ предмета-прискорення → скільки штук.</summary>
public record SpeedUpWithItemsRequest(EmpireIdle.Domain.Enums.SpeedUpTimer Timer, Guid TargetId, Dictionary<string, int> Items);
