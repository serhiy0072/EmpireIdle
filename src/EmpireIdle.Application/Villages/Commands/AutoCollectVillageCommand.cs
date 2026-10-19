using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Автозбір одного села (рішення 19.10.2026): вироблене переходить на склад саме. Одиниця роботи
    /// сканера: конфлікт паралелізму коштує це село, а не весь прогін, — наступний тік збере.
    /// </summary>
    public record AutoCollectVillageCommand(Guid VillageId) : IRequest;

    public sealed class AutoCollectVillageCommandHandler : IRequestHandler<AutoCollectVillageCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IServerRepository _serverRepository;
        private readonly EffectResolver _effectResolver;
        private readonly WorldGeometry _geometry;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public AutoCollectVillageCommandHandler(
            IVillageRepository villageRepository,
            IServerRepository serverRepository,
            EffectResolver effectResolver,
            WorldGeometry geometry,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _serverRepository = serverRepository;
            _effectResolver = effectResolver;
            _geometry = geometry;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task Handle(AutoCollectVillageCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Село могло зникнути між читанням id і обробкою — не помилка джоба
            var village = await _villageRepository.GetByIdAsync(request.VillageId, cancellationToken);
            if (village is null)
                return;

            var boost = await _effectResolver.GetProductionBoostAsync(village.PlayerId, now, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            village.AutoCollect(_catalog.Buildings, now, boost, locationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
