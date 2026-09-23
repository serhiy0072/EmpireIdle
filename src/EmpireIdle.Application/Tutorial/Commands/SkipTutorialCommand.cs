using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Tutorial.Commands
{
    /// <summary>Гравець відмовився від веденого туторіалу.</summary>
    public record SkipTutorialCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class SkipTutorialCommandHandler : IRequestHandler<SkipTutorialCommand>
    {
        private readonly ITutorialProgressRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SkipTutorialCommandHandler> _logger;

        public SkipTutorialCommandHandler(
            ITutorialProgressRepository repository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<SkipTutorialCommandHandler> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(SkipTutorialCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var progress = await _repository.GetByPlayerAsync(request.PlayerId, cancellationToken);

            if (progress is null)
            {
                progress = new TutorialProgress(Guid.NewGuid(), request.PlayerId, now);
                await _repository.AddAsync(progress, cancellationToken);
            }
            else if (progress.SkippedAt is not null)
            {
                return;
            }

            progress.Skip(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} skipped the tutorial", request.PlayerId);
        }
    }
}
