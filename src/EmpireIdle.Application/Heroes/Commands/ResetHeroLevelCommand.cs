using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Безкоштовне скидання рівня (GDD §6.1): герой повертається на перший рівень,
    /// весь вкладений досвід — у пул гравця без втрат. Помилку прокачки можна виправити,
    /// а досвід — перекинути на іншого героя.
    /// </summary>
    public record ResetHeroLevelCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class ResetHeroLevelCommandHandler : IRequestHandler<ResetHeroLevelCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HeroProgression _progression;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ResetHeroLevelCommandHandler> _logger;

        public ResetHeroLevelCommandHandler(
            IHeroRepository heroRepository,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            HeroProgression progression,
            TimeProvider timeProvider,
            ILogger<ResetHeroLevelCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _progression = progression;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(ResetHeroLevelCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId.ToString());

            // Герой у марші б'ється своїм рівнем; на ринку — продається за ним
            if (hero.State == HeroState.Deployed)
                throw new InvalidStateException(RefusalReasons.HeroOnTheMove, $"Hero {hero.Id} is on a march and cannot be reset.");

            // Скидати нічого — тихо нічого й не робимо: повтор кліку не має бути помилкою
            if (hero.Level <= 1)
                return;

            var refund = _progression.ResetRefund(hero.Level);
            var previous = hero.ResetLevel(now);

            if (refund > 0)
            {
                var pool = await _heroRepository.GetOrCreateExperienceAsync(request.PlayerId, _serverContext.ServerId, cancellationToken);
                pool.Add(refund);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} reset hero {HeroId} from level {Level}, {Refund} experience back to the pool",
                request.PlayerId, hero.Id, previous, refund);
        }
    }
}
