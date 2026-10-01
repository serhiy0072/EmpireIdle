using EmpireIdle.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Прибирає прострочені запити кланової допомоги. Bulk-операція, як і з ефектами:
    /// ExecuteDelete не вантажить агрегатів, внески йдуть каскадом.
    /// </summary>
    public record RemoveExpiredClanHelpCommand : IRequest;

    public sealed class RemoveExpiredClanHelpCommandHandler : IRequestHandler<RemoveExpiredClanHelpCommand>
    {
        private readonly IClanHelpRepository _helpRepository;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RemoveExpiredClanHelpCommandHandler> _logger;

        public RemoveExpiredClanHelpCommandHandler(
            IClanHelpRepository helpRepository,
            TimeProvider timeProvider,
            ILogger<RemoveExpiredClanHelpCommandHandler> logger)
        {
            _helpRepository = helpRepository;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RemoveExpiredClanHelpCommand request, CancellationToken cancellationToken)
        {
            var removed = await _helpRepository.RemoveExpiredAsync(_timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

            if (removed > 0)
                _logger.LogInformation("Removed {Count} expired clan help requests", removed);
        }
    }
}
