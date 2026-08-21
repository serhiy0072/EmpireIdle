using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>Прибирає стеки відновлюваних, у яких сплив дедлайн викупу.</summary>
    public record PurgeExpiredRecoverableCommand : IRequest;

    public sealed class PurgeExpiredRecoverableCommandHandler : IRequestHandler<PurgeExpiredRecoverableCommand>
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly ILogger<PurgeExpiredRecoverableCommandHandler> _logger;

        public PurgeExpiredRecoverableCommandHandler(
            IGarrisonRepository garrisonRepository,
            ILogger<PurgeExpiredRecoverableCommandHandler> logger)
        {
            _garrisonRepository = garrisonRepository;
            _logger = logger;
        }

        public async Task Handle(PurgeExpiredRecoverableCommand request, CancellationToken cancellationToken)
        {
            var removed = await _garrisonRepository.PurgeExpiredRecoverableAsync(DateTime.UtcNow, cancellationToken);

            if (removed > 0)
                _logger.LogInformation("Purged {Count} expired recoverable stacks", removed);
        }
    }
}
