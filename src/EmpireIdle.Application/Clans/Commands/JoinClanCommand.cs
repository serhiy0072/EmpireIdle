using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.Commands
{
    /// <summary>
    /// Вступ у клан. Заявки поки немає: приймаються лише відкриті клани,
    /// решта повертає відмову. Схвалення — окрема механіка з власною чергою.
    /// </summary>
    public record JoinClanCommand(Guid PlayerId, Guid ClanId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class JoinClanCommandHandler : IRequestHandler<JoinClanCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<JoinClanCommandHandler> _logger;

        public JoinClanCommandHandler(
            IClanRepository clanRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<JoinClanCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(JoinClanCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var player = await _playerRepository.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId);

            if (player.ClanId is not null)
                throw new InvalidStateException("Leave your current clan first.");

            var clan = await _clanRepository.GetByIdAsync(request.ClanId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan", request.ClanId);

            if (clan.JoinPolicy != ClanJoinPolicy.Open)
                throw new RequirementNotMetException("This clan does not accept open applications.");

            var clanConfig = _catalog.Config.Clan;
            clan.Join(player.Id, clan.Capacity(clanConfig.BaseCapacity, clanConfig.CapacityPerLevel), now);

            player.JoinClan(clan.Id);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} joined clan {ClanId}", player.Id, clan.Id);
        }
    }
}
