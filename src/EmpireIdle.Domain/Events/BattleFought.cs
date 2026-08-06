
namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: бій відбувся, результат відомий.</summary>
    public record BattleFought(Guid VillageId, Guid PlayerId, Guid MarchId, bool AttakerWon, int TargetX, int TargetY, string TerrainType, Dictionary<string, int> AttakerLosses) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
