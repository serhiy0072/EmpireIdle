using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dungeons.Commands
{
    /// <summary>
    /// Вихід із забігу. Енергію не повертаємо: інакше програшну спробу
    /// можна було б скасовувати перед смертю команди й ходити безкоштовно.
    /// </summary>
    public record AbandonDungeonRunCommand(Guid PlayerId, Guid RunId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class AbandonDungeonRunCommandHandler : IRequestHandler<AbandonDungeonRunCommand>
    {
        private readonly IDungeonRepository _dungeons;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AbandonDungeonRunCommandHandler> _logger;

        public AbandonDungeonRunCommandHandler(
            IDungeonRepository dungeons,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<AbandonDungeonRunCommandHandler> logger)
        {
            _dungeons = dungeons;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(AbandonDungeonRunCommand request, CancellationToken cancellationToken)
        {
            var run = await _dungeons.GetRunByIdAsync(request.RunId, cancellationToken)
                ?? throw new EntityNotFoundException("Dungeon run", request.RunId.ToString());

            if (run.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Dungeon run", request.RunId.ToString());

            // Уже завершений забіг покидати нічого — команда ідемпотентна
            if (run.State != DungeonRunState.InProgress)
                return;

            run.Abandon(_timeProvider.GetUtcNow().UtcDateTime);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} abandoned dungeon run {RunId}", request.PlayerId, run.Id);
        }
    }
}
