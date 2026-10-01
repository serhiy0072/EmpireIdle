using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Знімає неактивного лідера й ставить наступника. Фонова команда:
    /// виконавця немає, тож і PlayerScope тут не застосовний.
    /// </summary>
    public record TransferInactiveLeadershipCommand(Guid ClanId) : IRequest<bool>;

    /// <summary>Обробник TransferInactiveLeadershipCommand.</summary>
    public sealed class TransferInactiveLeadershipCommandHandler
        : IRequestHandler<TransferInactiveLeadershipCommand, bool>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ClanSuccession _succession;
        private readonly ILogger<TransferInactiveLeadershipCommandHandler> _logger;

        public TransferInactiveLeadershipCommandHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ClanSuccession succession,
            ILogger<TransferInactiveLeadershipCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _succession = succession;
            _logger = logger;
        }

        public async Task<bool> Handle(TransferInactiveLeadershipCommand request,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var inactiveSince = now.AddDays(-_catalog.Config.Clan.LeaderInactivityDays);

            var clan = await _clanRepository.GetByIdAsync(request.ClanId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan", request.ClanId);

            var leaderId = clan.LeaderId;

            if (leaderId is null)
                return false;

            var memberIds = clan.Members.Select(m => m.PlayerId).ToList();
            var presence = await _playerRepository.GetLastSeenAsync(memberIds, cancellationToken);

            // Лідер міг зайти між вибіркою джоба й цією командою
            if (presence.TryGetValue(leaderId.Value, out var leaderSeen) && leaderSeen >= inactiveSince)
                return false;

            // Активний найвищого рангу, а якщо активних немає взагалі — найстарший за рангом:
            // клан не має лишатись із лідером, який не повернеться. Правило спільне з виходом лідера
            if (await _succession.ChooseAsync(clan, leaderId.Value, inactiveSince, cancellationToken) is not Guid successorId)
            {
                _logger.LogInformation("Clan {ClanId} has no other members; leadership kept.", clan.Id);

                return false;
            }

            clan.PromoteToLeader(successorId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Clan {ClanId}: leadership moved from inactive {OldLeaderId} to {NewLeaderId}.",
                clan.Id, leaderId.Value, successorId);

            return true;
        }
    }
}
