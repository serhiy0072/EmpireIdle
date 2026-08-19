namespace EmpireIdle.Application.Quests
{
    /// <summary>
    /// Доменна подія, зведена до вигляду, зрозумілого квестам.
    /// Несе обидва числа: <paramref name="Increment"/> для цілей Accumulate
    /// і <paramref name="CurrentValue"/> для Threshold — режим задає конфіг, не подія.
    /// </summary>
    /// <param name="Target">Уточнення: ключ будівлі, тип юніта, результат бою. null — узагальнена подія.</param>
    /// <param name="CurrentValue">Поточне значення для порогових цілей; null — подія не має стану.</param>
    public record QuestSignal(Guid PlayerId, string EventType, string? Target, int Increment, int? CurrentValue);
}
