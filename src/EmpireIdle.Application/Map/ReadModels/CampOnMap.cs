namespace EmpireIdle.Application.Map.ReadModels
{
    /// <summary>
    /// Табір на мапі (§2.5): чия армія стоїть на клітинці. Склад не показується —
    /// як і в села, його дізнаються розвідкою.
    /// </summary>
    /// <param name="OwnerName">Назва села власника — під нею табір видно на мапі й у звітах.</param>
    public record CampOnMap(
        Guid MarchId,
        int X,
        int Y,
        Guid OwnerPlayerId,
        string OwnerName,
        Guid? OwnerClanId,
        string? OwnerClanTag);
}
