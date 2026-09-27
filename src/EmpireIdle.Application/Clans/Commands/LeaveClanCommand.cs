using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Гравець виходить із клану сам. Лідер лишає клан наступнику — найвищому за рангом,
    /// серед рівних найсвіжішому в грі; лідер, що лишився сам, розпускає клан.
    /// </summary>
    public record LeaveClanCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class LeaveClanCommandHandler : IRequestHandler<LeaveClanCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ReinforcementReturner _returner;
        private readonly ClanSuccession _succession;
        private readonly ILogger<LeaveClanCommandHandler> _logger;

        public LeaveClanCommandHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ReinforcementReturner returner,
            ClanSuccession succession,
            ILogger<LeaveClanCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _returner = returner;
            _succession = succession;
            _logger = logger;
        }

        public async Task Handle(LeaveClanCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            var clan = await _clanRepository.GetByMemberAsync(player.Id, cancellationToken)
                ?? throw new InvalidStateException(RefusalReasons.ClanNotMember, "You are not in a clan.");

            var successor = clan.IsLeader(player.Id)
                ? await _succession.ChooseAsync(clan, player.Id, activeSince: null, cancellationToken)
                : null;

            clan.Leave(player.Id, successor, now);
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

            if (successor is Guid newLeader)
                _logger.LogInformation("Clan {ClanId}: leader {PlayerId} left, leadership passed to {NewLeaderId}",
                    clan.Id, player.Id, newLeader);

            _logger.LogInformation("Player {PlayerId} left clan {ClanId}", player.Id, clan.Id);
        }
    }
}
