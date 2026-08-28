/// <summary>
/// Запит на використання предмета.
/// TargetId — для предметів, що діють на сутність; TargetX/TargetY — на клітину карти.
/// </summary>
public record UseItemRequest(string ItemKey, int Count, Guid? TargetId = null, int? TargetX = null, int? TargetY = null);
