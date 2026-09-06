namespace EmpireIdle.Application.Clans.ReadModels
{
    /// <summary>
    /// Заявка в черзі офіцера. Сила заявника поруч навмисно: рішення
    /// приймають саме за нею, і без неї довелося б відкривати профіль кожного.
    /// </summary>
    public record ClanApplicationItem(
        Guid RequestId,
        Guid PlayerId,
        string PlayerName,
        double Power,
        DateTime CreatedAt,
        DateTime ExpiresAt);
}
