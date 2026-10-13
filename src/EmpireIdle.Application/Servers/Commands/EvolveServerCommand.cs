using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Servers.Commands
{
    /// <summary>
    /// Розвиток світу (GDD §2.7, рішення 04.10.2026). Дві незалежні дії:
    /// рівень росте з часом і стелі не має — новий рівень відкриває контент;
    /// щільність закриває реєстрацію — заповнений світ не розтягується,
    /// натомість новачки йдуть у наступний.
    /// </summary>
    public record EvolveServerCommand(int ServerId) : IRequest;

    public sealed class EvolveServerCommandHandler : IRequestHandler<EvolveServerCommand>
    {
        private readonly IServerRepository _serverRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly WorldGeometry _geometry;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EvolveServerCommandHandler> _logger;

        public EvolveServerCommandHandler(
            IServerRepository serverRepository,
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            WorldGeometry geometry,
            TimeProvider timeProvider,
            ILogger<EvolveServerCommandHandler> logger)
        {
            _serverRepository = serverRepository;
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _geometry = geometry;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(EvolveServerCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var server = await _serverRepository.GetByIdAsync(request.ServerId, cancellationToken);

            if (server is null || server.State is ServerState.Sunset or ServerState.Archived)
                return;

            var evolution = _catalog.Config.Map.Evolution;
            var changed = false;

            if (server.AcceptsNewPlayers)
            {
                // Щільність від УСІЄЇ площі туману, не від придатної: непрохідні
                // клітини теж у знаменнику, і поріг калібрується з урахуванням цього
                var boundary = _geometry.SettlementBoundary(server.Level);
                var openArea = (boundary * 2 + 1) * (boundary * 2 + 1);
                var villages = await _villageRepository.CountAsync(cancellationToken);

                if ((double)villages / openArea >= evolution.DensityThreshold)
                {
                    server.CloseRegistration(now);
                    changed = true;

                    _logger.LogInformation(
                        "Server {ServerId} closed for registration: {Villages} villages in {Area} cells",
                        server.Id, villages, openArea);
                }
            }

            // Один рівень за прогін: джоб щоденний, і світ, що простояв без джоба,
            // наздожене за кілька днів, а не стрибком
            if (now - server.LevelSince >= TimeSpan.FromDays(evolution.DaysPerLevel))
            {
                server.RaiseLevel(now);
                changed = true;

                _logger.LogInformation("Server {ServerId} evolved to level {Level}", server.Id, server.Level);
            }

            if (changed)
                await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
