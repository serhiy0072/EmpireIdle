using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Domain.Exceptions;
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
    public record UseItemCommand(Guid PlayerId, string ItemKey, int Count, Guid? TargetId, int? TargetX = null, int? TargetY = null)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник UseItemCommand: застосовує ефект предмета й списує його з інвентаря.
    /// </summary>
    public sealed class UseItemCommandHandler : IRequestHandler<UseItemCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly ItemEffectDispatcher _dispatcher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UseItemCommandHandler> _logger;

        public UseItemCommandHandler(IInventoryRepository inventoryRepository, ItemEffectDispatcher dispatcher,
            IUnitOfWork unitOfWork, GameCatalog catalog, TimeProvider timeProvider, ILogger<UseItemCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _dispatcher = dispatcher;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UseItemCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (request.Count < 1)
                throw new RequirementNotMetException("Count must be positive.");

            var config = _catalog.FindItem(request.ItemKey)
                ?? throw new EntityNotFoundException("Item", request.ItemKey);

            var stack = await _inventoryRepository.GetItemAsync(request.PlayerId, request.ItemKey, cancellationToken)
                ?? throw new EntityNotFoundException("Item in inventory", request.ItemKey);

            // Ефект застосовуємо ДО списання: якщо він неможливий, предмет не згорить
            var context = new ItemUsageContext(
                request.PlayerId, config, request.Count, request.TargetId, now,
                request.TargetX, request.TargetY);

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
