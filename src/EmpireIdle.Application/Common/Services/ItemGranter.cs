using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Видає предмети гравцю: стакові додає до наявного стеку,
    /// створюючи його за потреби.
    /// </summary>
    public class ItemGranter
    {
        private readonly IInventoryRepository _repository;
        private readonly IServerContext _serverContext;
        private readonly IRandomSource _random;
        private readonly ArtifactRoller _roller;


        public ItemGranter(IInventoryRepository repository, IServerContext serverContext, IRandomSource random, ArtifactRoller roller)
        {
            _repository = repository;
            _random = random;
            _serverContext = serverContext;
            _roller = roller;
        }

        /// <summary>Видає стакові предмети.</summary>
        public async Task GrantAsync(Guid playerId, string itemKey, int count, CancellationToken cancellationToken = default)
        {
            if (count < 1)
                return;

            var existing = await _repository.GetItemAsync(playerId, itemKey, cancellationToken);

            if (existing is null)
            {
                await _repository.AddItemAsync(
                    new PlayerItem(Guid.NewGuid(), playerId, itemKey, count),
                    cancellationToken);
                return;
            }

            existing.Add(count);
        }

        /// <summary>
        /// Видає унікальний екземпляр спорядження.
        /// </summary>
        /// <param name="stats">
        /// Стати зброї. Для артефактів ігноруються: їхній набір рольований,
        /// і тип задає лише слот та належність до набору.
        /// </param>
        public async Task GrantEquipmentAsync(
            Guid playerId, string itemKey, EquipmentSlot slot, Rarity rarity,
            IEnumerable<(string Stat, double Value)> stats,
            DateTime utcNow, CancellationToken cancellationToken = default)
        {
            int? seed = slot == EquipmentSlot.Artifact ? _random.Next(int.MaxValue) : null;

            var rolled = seed is { } artifactSeed
                ? _roller.RollInitial(rarity, artifactSeed).Select(s => (s.Key, s.Value)).ToList()
                : stats;

            var item = new EquipmentItem(Guid.NewGuid(), playerId, _serverContext.ServerId,
                itemKey, slot, rarity, rolled, utcNow);

            // Стартовий набір теж іде в журнал: нульовий рівень відтворюється
            // так само, як і будь-яка подальша прокачка
            if (seed is { } initialSeed)
                item.RecordRoll(level: 0, initialSeed, utcNow);

            await _repository.AddEquipmentAsync(item, cancellationToken);
        }
    }
}
