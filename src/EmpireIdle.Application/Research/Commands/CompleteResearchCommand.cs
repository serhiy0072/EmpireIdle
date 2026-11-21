using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Research.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Research.Commands
{
    /// <summary>
    /// Завершує дозріле дослідження одного гравця. Одиниця роботи сканера:
    /// конфлікт паралелізму коштує цей запис, а не весь прогін.
    /// </summary>
    public record CompleteResearchCommand(Guid ResearchId) : IRequest;

    public sealed class CompleteResearchCommandHandler : IRequestHandler<CompleteResearchCommand>
    {
        private readonly IPlayerResearchRepository _research;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ResearchSettlement _settlement;
        private readonly ILogger<CompleteResearchCommandHandler> _logger;

        public CompleteResearchCommandHandler(IPlayerResearchRepository research, IUnitOfWork unitOfWork,
            TimeProvider timeProvider, ResearchSettlement settlement, ILogger<CompleteResearchCommandHandler> logger)
        {
            _settlement = settlement;
            _research = research;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CompleteResearchCommand request, CancellationToken cancellationToken)
        {
            var research = await _research.GetByIdAsync(request.ResearchId, cancellationToken);
            if (research is null)
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (research.CompletesAt is not { } completesAt || completesAt > now)
                return;

            await _settlement.SettleAsync(research, now, cancellationToken);

            var completed = research.CompleteDue(now);
            if (completed is null)
                return;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Player {PlayerId} finished researching {Key}", research.PlayerId, completed);
        }
    }
}
