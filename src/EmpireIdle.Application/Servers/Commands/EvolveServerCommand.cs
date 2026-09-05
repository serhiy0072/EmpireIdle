using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Servers.Commands
{
    /// <summary>
    /// Перевіряє, чи світ дозрів для наступного рівня, і закриває реєстрацію,
    /// коли він заповнився. Дві незалежні дії: рівень росте від зрілості,
    /// щільність закриває вхід — заповнений світ не розтягується, натомість
    /// новачки йдуть у наступний.
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

            if (server is null
                || server.State is ServerState.Sunset or ServerState.Archived
                || (!server.AcceptsNewPlayers && server.Level >= _catalog.Config.Map.MaxServerLevel))
                return;

            var evolution = _catalog.Config.Map.Evolution;
            var changed = false;

            // Щільність від УСІЄЇ площі туман, не від придатної: непрохідні
            // клітини теж у знаменнику, і поріг калібрується з урахуванням цього
            var boundary = _geometry.SettlementBoundary(server.Level);
            var openArea = (boundary * 2 + 1) * (boundary * 2 + 1);
            var villages = await _villageRepository.CountAsync(cancellationToken);

            if (server.AcceptsNewPlayers && (double)villages / openArea >= evolution.DensityThreshold)
            {
                server.CloseRegistration(now);
                changed = true;

                _logger.LogInformation(
                    "Server {ServerId} closed for registration: {Villages} villages in {Area} cells",
                    server.Id, villages, openArea);
            }

            if (CanRaiseLevel(server, evolution, now)
                && await IsMatureAsync(server, evolution, cancellationToken))
            {
                server.RaiseLevel(_catalog.Config.Map.MaxServerLevel, now);
                changed = true;

                _logger.LogInformation("Server {ServerId} evolved to level {Level}", server.Id, server.Level);
            }

            if (changed)
                await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Строк — нижня межа: без нього обидва рівні могли б стрибнути поспіль.</summary>
        private static bool CanRaiseLevel(Domain.Entities.Server server, ServerEvolutionConfig evolution, DateTime now)
        {
            // Світ, що ще не піднімався, відлічує строк від створення
            var since = server.LevelRaisedAt ?? server.CreatedAt;

            return now - since >= TimeSpan.FromDays(evolution.MinDaysBetweenLevels);
        }

        private async Task<bool> IsMatureAsync(Domain.Entities.Server server, ServerEvolutionConfig evolution,
            CancellationToken cancellationToken)
        {
            var median = await _villageRepository.GetMedianMainBuildingLevelAsync(
                _catalog.MainBuildingKey, cancellationToken);

            var ceiling = server.Level * _catalog.Config.BuildingLevelsPerTier;

            return median >= ceiling - evolution.MaturityMarginLevels;
        }
    }
}
