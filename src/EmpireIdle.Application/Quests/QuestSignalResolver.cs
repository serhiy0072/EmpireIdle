using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests
{
    /// <summary>
    /// Переводить доменні події у сигнали квестів.
    /// Одне місце, де знання «яка подія що дає квестам» зібране докупи —
    /// додати нову подію означає дописати одну гілку.
    /// </summary>
    public class QuestSignalResolver
    {
        private readonly IVillageRepository _villageRepository;
        private readonly ILogger<QuestSignalResolver> _logger;

        public QuestSignalResolver(IVillageRepository villageRepository, ILogger<QuestSignalResolver> logger)
        {
            _villageRepository = villageRepository;
            _logger = logger;
        }

        public async Task<QuestSignal?> ResolveAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
            => domainEvent switch
            {
                BuildingUpgradeCompleted e => new QuestSignal(
                    e.PlayerId, nameof(BuildingUpgradeCompleted), e.BuildingType, 1, e.NewLevel.Value),

                BuildingCollected e => new QuestSignal(
                    e.PlayerId, nameof(BuildingCollected), e.ResourceType, e.Amount, null),

                MonsterDefeated e => new QuestSignal(
                    e.PlayerId, nameof(MonsterDefeated), e.MonsterType, 1, null),

                BattleFought e => new QuestSignal(
                    e.PlayerId, nameof(BattleFought), e.Won ? "won" : "lost", 1, null),

                GemsSpent e => new QuestSignal(
                    e.PlayerId, nameof(GemsSpent), null, e.Amount.Value, null),

                // Гарнізон не знає гравця — резолвимо через село
                UnitsTrained e => await ResolveByVillageAsync(
                    e.VillageId, nameof(UnitsTrained), e.UnitType, e.Count, cancellationToken),

                _ => null
            };

        private async Task<QuestSignal?> ResolveByVillageAsync(Guid villageId, string eventType, string? target,
            int increment, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByIdAsync(villageId, cancellationToken);

            if (village is null)
            {
                _logger.LogWarning("Quest signal dropped: village {VillageId} not found.", villageId);
                return null;
            }

            return new QuestSignal(village.PlayerId, eventType, target, increment, null);
        }
    }
}
