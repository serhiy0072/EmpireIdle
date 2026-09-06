using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>Гравець виходить із клану сам. Лідер спершу передає лідерство.</summary>
    public record LeaveClanCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class LeaveClanCommandHandler : IRequestHandler<LeaveClanCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ReinforcementReturner _returner;
        private readonly ILogger<LeaveClanCommandHandler> _logger;

        public LeaveClanCommandHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ReinforcementReturner returner,
            ILogger<LeaveClanCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _returner = returner;   
            _logger = logger;
        }

        public async Task Handle(LeaveClanCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            var clan = await _clanRepository.GetByMemberAsync(player.Id, cancellationToken)
                ?? throw new InvalidStateException("You are not in a clan.");

            clan.Leave(player.Id, now);
            player.LeaveClan();

            // Обидва напрямки: свої війська з чужих сіл і чужі — зі свого.
            // Поза кланом гість у селі не має підстав лишатися
            await _returner.ReturnAllOfPlayerAsync(player.Id, now, cancellationToken);

            var village = await _villageRepository.GetByPlayerIdAsync(player.Id, cancellationToken);

            if (village is not null)
                await _returner.ReturnAllFromVillageAsync(village.Id, now, cancellationToken);

            // Останній учасник — клан зникає. Порожній клан тримав би
            // назву й тег зайнятими назавжди
            if (clan.Members.Count == 0)
            {
                _clanRepository.Remove(clan);
                _logger.LogInformation("Clan {ClanId} disbanded: last member left", clan.Id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} left clan {ClanId}", player.Id, clan.Id);
        }
    }
}
