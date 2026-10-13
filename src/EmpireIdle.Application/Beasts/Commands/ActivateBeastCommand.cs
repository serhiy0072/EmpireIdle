using EmpireIdle.Application.Beasts.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Beasts.Commands
{
    /// <summary>
    /// Увімкнути пасивку звіра (GDD §5.10): коштує їжі зі складу, діє обмежений час,
    /// потім перезаряджається. Прискорити перезарядку за gems не можна.
    /// </summary>
    public record ActivateBeastCommand(Guid PlayerId, string BeastKey)
        : IRequest<BeastActivatedView>, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class ActivateBeastCommandHandler : IRequestHandler<ActivateBeastCommand, BeastActivatedView>
    {
        /// <summary>Чим платять за активацію — їжею зі складу, стоком для запасів без стелі.</summary>
        private const string ActivationResource = "food";

        private readonly IBeastPenRepository _pens;
        private readonly IVillageRepository _villages;
        private readonly IServerRepository _servers;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EffectResolver _effects;
        private readonly WorldGeometry _geometry;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ActivateBeastCommandHandler> _logger;

        public ActivateBeastCommandHandler(IBeastPenRepository pens, IVillageRepository villages, IServerRepository servers,
            IUnitOfWork unitOfWork, EffectResolver effects, WorldGeometry geometry, GameCatalog catalog, TimeProvider timeProvider,
            ILogger<ActivateBeastCommandHandler> logger)
        {
            _pens = pens;
            _villages = villages;
            _servers = servers;
            _unitOfWork = unitOfWork;
            _effects = effects;
            _geometry = geometry;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<BeastActivatedView> Handle(ActivateBeastCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var config = _catalog.Beasts.GetValueOrDefault(request.BeastKey)
                ?? throw new EntityNotFoundException("Beast", request.BeastKey);

            var pen = await _pens.GetByPlayerAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Beast", request.BeastKey);

            var village = await _villages.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Вікно звіра зберігається одне: нова активація затирає попередню. Тож виробіток
            // за старим вікном фіксуємо до того, як воно зникне, — так само, як для буста крамниці
            if (config.Effect == EffectTarget.Production)
                await MaterializeProductionAsync(request.PlayerId, village, now, cancellationToken);

            pen.Activate(request.BeastKey, EffectOf, TimeSpan.FromMinutes(config.DurationMinutes),
                TimeSpan.FromMinutes(config.CooldownMinutes), now);

            village.ChargeCost([new ResourceCost { Resource = ActivationResource, Amount = config.ActivationFood }], now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var beast = pen.Beasts.Single(b => b.BeastKey == request.BeastKey);

            _logger.LogInformation("Player {PlayerId} activated beast {BeastKey} until {ActiveUntil:u}",
                request.PlayerId, request.BeastKey, beast.ActiveUntil);

            return new BeastActivatedView(request.BeastKey, beast.ActiveUntil!.Value, beast.CooldownUntil!.Value);
        }

        private EffectTarget EffectOf(string beastKey) => _catalog.Beasts[beastKey].Effect;

        private async Task MaterializeProductionAsync(Guid playerId, Domain.Entities.Village village, DateTime utcNow,
            CancellationToken cancellationToken)
        {
            var boost = await _effects.GetProductionBoostAsync(playerId, utcNow, cancellationToken);
            var serverLevel = await _servers.GetLevelAsync(village.ServerId, cancellationToken);

            village.MaterializeProduction(_catalog.Buildings, utcNow, boost,
                _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel));
        }
    }
}
