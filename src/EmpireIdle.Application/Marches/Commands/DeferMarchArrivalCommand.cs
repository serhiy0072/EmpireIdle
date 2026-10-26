using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>
    /// Відкладає прибуття, що впало. Викликається сканером у свіжому scope після помилки
    /// CompleteMarchCommand: транзакція того вже відкочена, а контекст — брудний.
    /// </summary>
    public record DeferMarchArrivalCommand(Guid MarchId) : IRequest;

    public sealed class DeferMarchArrivalCommandHandler : IRequestHandler<DeferMarchArrivalCommand>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DeferMarchArrivalCommandHandler> _logger;

        public DeferMarchArrivalCommandHandler(
            IMarchRepository marchRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<DeferMarchArrivalCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DeferMarchArrivalCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var march = await _marchRepository.GetByIdAsync(request.MarchId, cancellationToken);

            // Паралельний прогін міг таки завершити похід
            if (march is null || march.State == MarchState.Completed)
                return;

            march.DeferArrival(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("March {MarchId} arrival failed; next attempt at {RetryAt:O}", march.Id, march.ArrivalRetryAt);
        }
    }
}
