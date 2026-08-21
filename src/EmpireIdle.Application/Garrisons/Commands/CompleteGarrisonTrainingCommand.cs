using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>
    /// Завершує дозрілі тренування одного гарнізону. Одиниця роботи сканера:
    /// конфлікт паралелізму коштує цей гарнізон, а не весь прогін.
    /// </summary>
    public record CompleteGarrisonTrainingCommand(Guid GarrisonId) : IRequest;

    public sealed class CompleteGarrisonTrainingCommandHandler : IRequestHandler<CompleteGarrisonTrainingCommand>
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CompleteGarrisonTrainingCommandHandler> _logger;

        public CompleteGarrisonTrainingCommandHandler(
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<CompleteGarrisonTrainingCommandHandler> logger)
        {
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CompleteGarrisonTrainingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var garrison = await _garrisonRepository.GetByIdAsync(request.GarrisonId, cancellationToken);
            if (garrison is null)
                return;

            var trained = garrison.CompleteDueTraining(now);
            if (trained == 0)
                return;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Completed {Count} trainings in garrison {GarrisonId}", trained, garrison.Id);
        }
    }
}
