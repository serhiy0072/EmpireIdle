using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Посилення артефакта (GDD §6.4): згодувати йому інше спорядження, крім унікального, і гаєчки.
    /// Згодований прокачаний артефакт передає весь вкладений досвід; попередження перед цим — у клієнті.
    /// </summary>
    /// <param name="FoodIds">Спорядження, яке згодовується, — воно зникає.</param>
    /// <param name="Wrenches">Ключ гаєчки → скільки штук.</param>
    public record FeedArtifactCommand(Guid PlayerId, Guid EquipmentId, IReadOnlyList<Guid> FoodIds,
        IReadOnlyDictionary<string, int> Wrenches) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник FeedArtifactCommand. Кожен пройдений рівень розігрує свій ролл статів (на 4/8 — новий стат,
    /// на 12/16/20 — підсилення): сід лягає в журнал предмета, тож ролл відтворюваний.
    /// Досвід понад стелю рівня згорає — як надлишок прискорення.
    /// </summary>
    public sealed class FeedArtifactCommandHandler : IRequestHandler<FeedArtifactCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ArtifactProgression _progression;
        private readonly ArtifactRoller _roller;
        private readonly IRandomSource _random;
        private readonly GameCatalog _catalog;
        private readonly ILogger<FeedArtifactCommandHandler> _logger;

        public FeedArtifactCommandHandler(
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ArtifactProgression progression,
            ArtifactRoller roller,
            IRandomSource random,
            GameCatalog catalog,
            ILogger<FeedArtifactCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _progression = progression;
            _roller = roller;
            _random = random;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(FeedArtifactCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var item = await OwnedAsync(request.PlayerId, request.EquipmentId, cancellationToken);
            item.EnsureNotOnMarket();

            if (item.Level >= _progression.MaxLevel)
                throw new RequirementNotMetException(RefusalReasons.EquipmentMaxLevel,
                    $"Artifact {item.Id} is already at level {item.Level}.", _progression.MaxLevel);

            long experience = 0;
            var foods = new List<EquipmentItem>();

            foreach (var foodId in request.FoodIds.Distinct())
            {
                if (foodId == item.Id)
                    throw new RequirementNotMetException($"Artifact {item.Id} cannot be fed to itself.");

                var food = await OwnedAsync(request.PlayerId, foodId, cancellationToken);
                food.EnsureCanBeFed();

                experience += _progression.FeedValue(food)
                    ?? throw new RequirementNotMetException(RefusalReasons.EquipmentUniqueFood,
                        $"Equipment {food.Id} is {food.Rarity} and cannot be fed.", _catalog.FindItem(food.ItemKey)?.DisplayName ?? food.ItemKey);

                foods.Add(food);
            }

            var stacks = new List<(PlayerItem Stack, int Count)>();

            foreach (var (key, count) in request.Wrenches)
            {
                var wrench = _catalog.FindItem(key);

                if (wrench is null || wrench.Type != "wrench")
                    throw new RequirementNotMetException($"Item '{key}' is not a wrench.");

                var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, key, cancellationToken);

                if (stack is null || stack.Count < count)
                    throw new RequirementNotMetException($"Not enough '{key}': {stack?.Count ?? 0} of {count}.");

                experience += (long)wrench.ArtifactExperience * count;
                stacks.Add((stack, count));
            }

            // Досвід понад стелю згорає: інакше предмет на стелі накопичував би невидимий запас
            var cap = _progression.ExperienceToReach(_progression.MaxLevel) - item.Experience;
            var gained = Math.Min(experience, cap);
            var before = item.Level;
            var after = _progression.LevelFor(item.Experience + gained);

            item.GainExperience(gained, after, now);

            var setKey = _catalog.FindItem(item.ItemKey)?.SetKey;

            for (var level = before + 1; level <= after; level++)
            {
                var seed = _random.Next(int.MaxValue);
                var roll = _roller.RollForLevel(level, item.Rarity, setKey, item.Stats.Select(s => s.StatKey).ToList(), seed);

                foreach (var (stat, value) in roll.Added)
                    item.AddStat(stat, value, now);

                foreach (var (stat, delta) in roll.Raised)
                    item.RaiseStat(stat, delta, now);

                item.RecordRoll(level, seed, now);
            }

            foreach (var food in foods)
                _inventoryRepository.RemoveEquipment(food);

            foreach (var (stack, count) in stacks)
            {
                stack.Consume(count);

                if (stack.Count == 0)
                    _inventoryRepository.RemoveItem(stack);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Artifact {EquipmentId} fed {Experience} experience: level {Before} → {After}",
                item.Id, gained, before, after);
        }

        /// <summary>Свій предмет; чужий не відрізняється від неіснуючого.</summary>
        private async Task<EquipmentItem> OwnedAsync(Guid playerId, Guid equipmentId, CancellationToken cancellationToken)
        {
            var item = await _inventoryRepository.GetEquipmentByIdAsync(equipmentId, cancellationToken);

            if (item is null || item.PlayerId != playerId)
                throw new EntityNotFoundException("Equipment", equipmentId.ToString());

            return item;
        }
    }
}
