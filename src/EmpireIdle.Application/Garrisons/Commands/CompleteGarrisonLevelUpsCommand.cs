using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>
    /// Завершує дозрілі прокачки одного гарнізону. Одиниця роботи сканера:
    /// конфлікт паралелізму коштує цей гарнізон, а не весь прогін.
    /// </summary>
    public record CompleteGarrisonLevelUpsCommand(Guid GarrisonId) : IRequest;

    public sealed class CompleteGarrisonLevelUpsCommandHandler : IRequestHandler<CompleteGarrisonLevelUpsCommand>
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CompleteGarrisonLevelUpsCommandHandler> _logger;

        public CompleteGarrisonLevelUpsCommandHandler(
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<CompleteGarrisonLevelUpsCommandHandler> logger)
        {
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CompleteGarrisonLevelUpsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var garrison = await _garrisonRepository.GetByIdAsync(request.GarrisonId, cancellationToken);
            if (garrison is null)
                return;

            var completed = garrison.CompleteDueLevelUps(now);
            if (completed == 0)
                return;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Completed {Count} level-ups in garrison {GarrisonId}", completed, garrison.Id);
        }
    }
}
