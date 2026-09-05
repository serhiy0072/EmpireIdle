using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Чим скінчився вступ. Результат команди, а не стан домену, тому
    /// лежить тут, а не в Domain/Enums — як HealPaymentMethod.
    /// </summary>
    public enum ClanJoinOutcome
    {
        Joined = 0,
        ApplicationSubmitted = 1
    }

    /// <summary>
    /// Вступ у клан. Що станеться, вирішує політика клану: у відкритий
    /// гравець заходить одразу, у закритий подає заявку, у клан за
    /// запрошеннями не потрапляє взагалі — там вхід через запрошення.
    /// </summary>
    public record JoinClanCommand(Guid PlayerId, Guid ClanId)
        : IRequest<ClanJoinOutcome>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник JoinClanCommand.</summary>
    public sealed class JoinClanCommandHandler : IRequestHandler<JoinClanCommand, ClanJoinOutcome>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanRequestRepository _requestRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<JoinClanCommandHandler> _logger;

        public JoinClanCommandHandler(
            IClanRepository clanRepository,
            IClanRequestRepository requestRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<JoinClanCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _requestRepository = requestRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ClanJoinOutcome> Handle(JoinClanCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            if (player.ClanId is not null)
                throw new InvalidStateException("Leave your current clan first.");

            var clan = await _clanRepository.GetByIdAsync(request.ClanId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan", request.ClanId);

            var clanConfig = _catalog.Config.Clan;

            if (clan.JoinPolicy == ClanJoinPolicy.InviteOnly)
                throw new RequirementNotMetException("This clan is invite-only.");

            if (clan.JoinPolicy == ClanJoinPolicy.ByApproval)
            {
                await SubmitApplicationAsync(clan, player.Id, now, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Player {PlayerId} applied to clan {ClanId}", player.Id, clan.Id);

                return ClanJoinOutcome.ApplicationSubmitted;
            }

            // Членство — в агрегаті: там же перевірка місткості й видача
            // ролі за замовчуванням. Player.ClanId — лише дзеркало
            clan.Join(player.Id, clanConfig.Capacity, now);
            player.JoinClan(clan.Id);

            await CloseOtherRequestsAsync(player.Id, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} joined clan {ClanId}", player.Id, clan.Id);

            return ClanJoinOutcome.Joined;
        }

        /// <summary>
        /// Створює заявку, якщо гравцеві зараз можна її подати: попередня
        /// не висить відкритою і кулдаун після відмови минув.
        /// </summary>
        private async Task SubmitApplicationAsync(Clan clan, Guid playerId, DateTime now,
            CancellationToken cancellationToken)
        {
            var clanConfig = _catalog.Config.Clan;

            var previous = await _requestRepository.GetLatestAsync(
                clan.Id, playerId, ClanRequestKind.Application, cancellationToken);

            if (previous is not null)
            {
                if (previous.IsPending(now))
                    throw new AlreadyExistsException("Clan application", clan.Id.ToString());

                // Кулдаун рахується від моменту відмови, а не від подання:
                // інакше довга черга офіцерів здешевлювала б повторну спробу
                if (previous.Status == ClanRequestStatus.Declined
                    && previous.ResolvedAt is { } resolvedAt
                    && now < resolvedAt.AddHours(clanConfig.RejectedCooldownHours))
                {
                    var retryAt = resolvedAt.AddHours(clanConfig.RejectedCooldownHours);

                    throw new RequirementNotMetException($"You can apply to this clan again after {retryAt:u}.");
                }
            }

            var application = new ClanRequest(
                Guid.NewGuid(), clan.ServerId, clan.Id, playerId, ClanRequestKind.Application,
                now.AddHours(clanConfig.RequestLifetimeHours), now);

            await _requestRepository.AddAsync(application, cancellationToken);
        }

        /// <summary>
        /// Знімає решту відкритих заявок і запрошень гравця: він уже
        /// в клані, і чинними вони бути не можуть.
        /// </summary>
        private async Task CloseOtherRequestsAsync(Guid playerId, DateTime now, CancellationToken cancellationToken)
        {
            var pending = await _requestRepository.GetPendingForPlayerAsync(playerId, now, cancellationToken);

            foreach (var item in pending)
                item.Cancel(playerId, now);
        }
    }
}
