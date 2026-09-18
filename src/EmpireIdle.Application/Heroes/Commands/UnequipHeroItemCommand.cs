using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>Зняти спорядження з героя в інвентар.</summary>
    public record UnequipHeroItemCommand(Guid PlayerId, Guid EquipmentId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник UnequipHeroItemCommand.
    ///
    /// Героя тут не питаємо: предмет сам знає, на кому вдягнений, а власника
    /// перевіряє сам предмет. Зняття з героя в дорозі теж дозволене — інакше
    /// зламаний або застряглий предмет неможливо було б звільнити, поки
    /// герой не повернеться.
    /// </summary>
    public sealed class UnequipHeroItemCommandHandler : IRequestHandler<UnequipHeroItemCommand>
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UnequipHeroItemCommandHandler> _logger;

        public UnequipHeroItemCommandHandler(
            IInventoryRepository inventoryRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<UnequipHeroItemCommandHandler> logger)
        {
            _inventoryRepository = inventoryRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(UnequipHeroItemCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var item = await _inventoryRepository.GetEquipmentByIdAsync(request.EquipmentId, cancellationToken)
                ?? throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            if (item.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Equipment", request.EquipmentId.ToString());

            // Уже в інвентарі — команда ідемпотентна
            if (item.EquippedByHeroId is null)
                return;

            var heroId = item.EquippedByHeroId.Value;

            item.Unequip(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Equipment {EquipmentId} removed from hero {HeroId}", item.Id, heroId);
        }
    }
}
