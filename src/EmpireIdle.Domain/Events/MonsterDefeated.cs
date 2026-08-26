using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: монстра переможено, нагороди зараховані в село.</summary>
    public record MonsterDefeated(Guid VillageId, Guid PlayerId, Guid MarchId, string MonsterType,
        int MonsterLevel, List<ResourceCost> Rewards, DateTime OccurredAt) : IDomainEvent;

}
