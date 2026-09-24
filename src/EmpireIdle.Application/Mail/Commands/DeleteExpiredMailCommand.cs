using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Mail.Commands
{
    /// <summary>Прибирає протерміновані листи й оголошення поточного світу. Системна команда джоба.</summary>
    public record DeleteExpiredMailCommand : IRequest;

    public sealed class DeleteExpiredMailCommandHandler : IRequestHandler<DeleteExpiredMailCommand>
    {
        private readonly IMailRepository _mail;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DeleteExpiredMailCommandHandler> _logger;

        public DeleteExpiredMailCommandHandler(IMailRepository mail, TimeProvider timeProvider,
            ILogger<DeleteExpiredMailCommandHandler> logger)
        {
            _mail = mail;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DeleteExpiredMailCommand request, CancellationToken cancellationToken)
        {
            var deleted = await _mail.DeleteExpiredAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            _logger.LogInformation("Deleted {Count} expired letters and announcements", deleted);
        }
    }
}
