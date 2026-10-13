using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Speedups.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Speedups.Commands
{
    /// <summary>
    /// Прискорити таймер предметами (рішення 08.10.2026): кілька прискорень за раз, хвилини
    /// складаються. Універсальні — на будь-який таймер.
    /// </summary>
    /// <param name="Items">Ключ предмета-прискорення → скільки штук витратити.</param>
    public record SpeedUpWithItemsCommand(Guid PlayerId, SpeedUpTimer Timer, Guid TargetId,
        IReadOnlyDictionary<string, int> Items) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SpeedUpWithItemsCommand.
    ///
    /// Межа таймера та сама, що й для gems: предмет не зрізає нижче неї, а надлишок хвилин
    /// згорає — як у gems, «здачі» немає. Вибір набору без перебору робить клієнт.
    /// </summary>
    public sealed class SpeedUpWithItemsCommandHandler : IRequestHandler<SpeedUpWithItemsCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly SpeedUpTargets _targets;
        private readonly SpeedUpCalculator _calculator;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SpeedUpWithItemsCommandHandler> _logger;

        public SpeedUpWithItemsCommandHandler(IInventoryRepository inventoryRepository, SpeedUpTargets targets,
            SpeedUpCalculator calculator, GameCatalog catalog, IUnitOfWork unitOfWork, TimeProvider timeProvider,
            ILogger<SpeedUpWithItemsCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _targets = targets;
            _calculator = calculator;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(SpeedUpWithItemsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var minutes = 0L;
            var stacks = new List<(Domain.Entities.PlayerItem Stack, int Count)>();

            foreach (var (itemKey, count) in request.Items)
            {
                var config = _catalog.FindItem(itemKey);

                if (config is null || config.Type != "speedup")
                    throw new RequirementNotMetException($"Item '{itemKey}' is not a speed-up.");

                var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, itemKey, cancellationToken);

                if (stack is null || stack.Count < count)
                    throw new RequirementNotMetException($"Not enough '{itemKey}': {stack?.Count ?? 0} of {count}.");

                minutes += (long)config.SpeedUpMinutes * count;
                stacks.Add((stack, count));
            }

            var target = await _targets.FindAsync(request.PlayerId, request.Timer, request.TargetId, cancellationToken);

            // Нижче межі не зрізаємо; на межі — відмова, і предмети не згорають
            var available = _calculator.RequireCut(request.Timer, target.CompletesAt, now);
            var cut = TimeSpan.FromMinutes(minutes) < available ? TimeSpan.FromMinutes(minutes) : available;

            target.Apply(cut, now);

            foreach (var (stack, count) in stacks)
            {
                stack.Consume(count);

                if (stack.Count == 0)
                    _inventoryRepository.RemoveItem(stack);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up {Timer} {TargetId} by {Minutes:F1} min with items",
                request.PlayerId, request.Timer, request.TargetId, cut.TotalMinutes);
        }
    }
}
