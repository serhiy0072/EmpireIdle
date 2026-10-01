using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>
    /// Прибирає завершені марші поточного світу. Системна команда джоба.
    /// Завершений марш ніхто не читає: історію бою тримає звіт, а клієнт показує лише активні.
    /// </summary>
    public record DeleteCompletedMarchesCommand : IRequest;

    public sealed class DeleteCompletedMarchesCommandHandler : IRequestHandler<DeleteCompletedMarchesCommand>
    {
        /// <summary>
        /// Запас після завершення: обробники подій з outbox і повтор CompleteMarchCommand
        /// ще можуть шукати марш за id, а доба дає час розібрати інцидент за свіжими даними.
        /// </summary>
        public static readonly TimeSpan Retention = TimeSpan.FromDays(1);

        private readonly IMarchRepository _marches;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DeleteCompletedMarchesCommandHandler> _logger;

        public DeleteCompletedMarchesCommandHandler(IMarchRepository marches, TimeProvider timeProvider,
            ILogger<DeleteCompletedMarchesCommandHandler> logger)
        {
            _marches = marches;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DeleteCompletedMarchesCommand request, CancellationToken cancellationToken)
        {
            var deleted = await _marches.DeleteCompletedBeforeAsync(
                _timeProvider.GetUtcNow().UtcDateTime - Retention, cancellationToken);

            _logger.LogInformation("Deleted {Count} completed marches", deleted);
        }
    }
}
