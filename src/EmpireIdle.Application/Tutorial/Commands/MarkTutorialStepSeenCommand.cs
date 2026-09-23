using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Tutorial.Commands
{
    /// <summary>Гравець побачив підказку: більше її не показувати, на жодному пристрої.</summary>
    public record MarkTutorialStepSeenCommand(Guid PlayerId, string StepKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник MarkTutorialStepSeenCommand. Рядок прогресу створюється
    /// лінивo на першому кроці: реєстрація про навчання не знає.
    /// Гонку двох перших кроків з різних вкладок розв'язує унікальний
    /// індекс за PlayerId — програвший отримує AlreadyExists і повторює.
    /// </summary>
    public sealed class MarkTutorialStepSeenCommandHandler : IRequestHandler<MarkTutorialStepSeenCommand>
    {
        private readonly ITutorialProgressRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<MarkTutorialStepSeenCommandHandler> _logger;

        public MarkTutorialStepSeenCommandHandler(
            ITutorialProgressRepository repository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<MarkTutorialStepSeenCommandHandler> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(MarkTutorialStepSeenCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var progress = await _repository.GetByPlayerAsync(request.PlayerId, cancellationToken);

            if (progress is null)
            {
                progress = new TutorialProgress(Guid.NewGuid(), request.PlayerId, now);
                await _repository.AddAsync(progress, cancellationToken);
            }

            // Повтор нічого не пише: зайвий UPDATE лише підняв би xmin і зіштовхнув сусідню вкладку
            if (!progress.MarkSeen(request.StepKey, now))
                return;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} saw tutorial step {StepKey}", request.PlayerId, request.StepKey);
        }
    }
}
