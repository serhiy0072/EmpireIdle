using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Inventory.Commands
{
    /// <summary>
    /// Використати предмет з інвентаря.
    /// TargetId — необов'язкова ціль (для предметів, що діють на конкретний об'єкт).
    /// </summary>
    public record UseItemCommand(Guid PlayerId, string ItemKey, int Count, Guid? TargetId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник UseItemCommand: застосовує ефект предмета й списує його з інвентаря.
    /// </summary>
    public class UseItemCommandHandler : IRequestHandler<UseItemCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ItemEffectDispatcher _dispatcher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameConfig _gameConfig;
        private readonly ILogger<UseItemCommandHandler> _logger;

        public UseItemCommandHandler(IInventoryRepository inventoryRepository, ItemEffectDispatcher dispatcher,
            IUnitOfWork unitOfWork, IOptions<GameConfig> gameConfig, ILogger<UseItemCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _dispatcher = dispatcher;
            _unitOfWork = unitOfWork;
            _gameConfig = gameConfig.Value;
            _logger = logger;
        }

        public async Task Handle(UseItemCommand request, CancellationToken cancellationToken)
        {
            if (request.Count < 1)
                throw new InvalidOperationException("Count must be positive.");

            var config = _gameConfig.Items.FirstOrDefault(i => i.Key == request.ItemKey)
                ?? throw new InvalidOperationException($"Unknown item '{request.ItemKey}'.");

            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, request.ItemKey, cancellationToken)
                ?? throw new InvalidOperationException($"Item '{request.ItemKey}' not found in inventory.");

            // Ефект застосовуємо ДО списання: якщо він неможливий, предмет не згорить
            var context = new ItemUsageContext(
                request.PlayerId, config, request.Count, request.TargetId, DateTime.UtcNow);

            await _dispatcher.ApplyAsync(context, cancellationToken);

            stack.Consume(request.Count);

            if (stack.Count == 0)
                _inventoryRepository.RemoveItem(stack);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} used {Count}x {ItemKey}",
                request.PlayerId, request.Count, request.ItemKey);
        }
    }
}