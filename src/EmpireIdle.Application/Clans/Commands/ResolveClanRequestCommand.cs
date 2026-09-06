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
    /// Рішення по заявці або запрошенню.
    ///
    /// Одна команда на обидва види: перехід «прийняли — вступив» той самий,
    /// різниться лише те, хто має право вирішувати. Розводити на дві
    /// означало б мати дві копії вступу.
    /// </summary>
    public record ResolveClanRequestCommand(Guid PlayerId, Guid RequestId, bool Approve)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>Обробник ResolveClanRequestCommand.</summary>
    public sealed class ResolveClanRequestCommandHandler : IRequestHandler<ResolveClanRequestCommand>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanRequestRepository _requestRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ResolveClanRequestCommandHandler> _logger;

        public ResolveClanRequestCommandHandler(
            IClanRepository clanRepository,
            IClanRequestRepository requestRepository,
            IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<ResolveClanRequestCommandHandler> logger)
        {
            _clanRepository = clanRepository;
            _requestRepository = requestRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(ResolveClanRequestCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var clanRequest = await _requestRepository.GetByIdAsync(request.RequestId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan request", request.RequestId);

            var clan = await _clanRepository.GetByIdAsync(clanRequest.ClanId, cancellationToken)
                ?? throw new EntityNotFoundException("Clan", clanRequest.ClanId);

            // Заявку вирішує клан, запрошення — той, кого запросили
            if (clanRequest.Kind == ClanRequestKind.Application)
                clan.EnsureCan(request.PlayerId, ClanPermission.Recruit);
            else if (clanRequest.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Clan request", request.RequestId);

            if (!request.Approve)
            {
                clanRequest.Decline(request.PlayerId, now);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Clan request {RequestId} declined by {ActorId}",
                    clanRequest.Id, request.PlayerId);

                return;
            }

            var player = await _playerRepository.GetByIdAsync(clanRequest.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", clanRequest.PlayerId);

            // Між поданням і рішенням гравець міг вступити деінде
            if (player.ClanId is not null)
                throw new RequirementNotMetException("This player has joined another clan.");

            clanRequest.Accept(request.PlayerId, now);

            clan.Join(player.Id, _catalog.Config.Clan.Capacity, now);
            player.JoinClan(clan.Id);

            var pending = await _requestRepository.GetPendingForPlayerAsync(player.Id, now, cancellationToken);

            foreach (var item in pending.Where(r => r.Id != clanRequest.Id))
                item.Cancel(request.PlayerId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} joined clan {ClanId} via {Kind} {RequestId}",
                player.Id, clan.Id, clanRequest.Kind, clanRequest.Id);
        }
    }
}
