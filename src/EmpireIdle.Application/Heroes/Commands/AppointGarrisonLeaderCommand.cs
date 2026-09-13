using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>
    /// Призначити героя лідером гарнізону, у якому він стоїть.
    /// </summary>
    public record AppointGarrisonLeaderCommand(Guid PlayerId, Guid HeroId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник AppointGarrisonLeaderCommand: знімає лідерство з попереднього
    /// й вішає на нового в тому самому гарнізоні.
    ///
    /// Слот один на гравця в гарнізоні, тому попередній складається тут же,
    /// в одній транзакції: інакше частковий унікальний індекс відкинув би
    /// вставку, і гравець побачив би 409 замість зміни лідера.
    /// </summary>
    public sealed class AppointGarrisonLeaderCommandHandler : IRequestHandler<AppointGarrisonLeaderCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AppointGarrisonLeaderCommandHandler> _logger;

        public AppointGarrisonLeaderCommandHandler(
            IHeroRepository heroRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<AppointGarrisonLeaderCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(AppointGarrisonLeaderCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var hero = await _heroRepository.GetByIdAsync(request.HeroId, cancellationToken)
                ?? throw new EntityNotFoundException("Hero", request.HeroId);

            if (hero.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Hero", request.HeroId);

            var garrisonId = hero.StationedGarrisonId
                ?? throw new RequirementNotMetException($"Hero {request.HeroId} is on the move.");

            if (hero.IsLeader)
                return;

            var previous = await _heroRepository.GetLeaderAsync(garrisonId, request.PlayerId, cancellationToken);

            previous?.DismissLeader(now);

            hero.AppointLeader(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Hero {HeroId} leads garrison {GarrisonId} for player {PlayerId}",
                hero.Id, garrisonId, request.PlayerId);
        }
    }
}
