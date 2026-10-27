namespace EmpireIdle.API.DTOs;

/// <summary>Результат «Зібрати все»: забрані квести; порожньо — нічого було забирати.</summary>
public record ClaimAllQuestsResponse(List<string> ClaimedKeys);
