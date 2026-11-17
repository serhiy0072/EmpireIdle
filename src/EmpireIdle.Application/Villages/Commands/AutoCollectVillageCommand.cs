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
        private readonly IUnitOfWork _unitOfWork;
        private readonly VillageProductionRates _rates;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public AutoCollectVillageCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            VillageProductionRates rates,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _rates = rates;
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

            var rates = await _rates.ForAsync(village, now, cancellationToken);

            village.AutoCollect(_catalog.Buildings, now, rates.Boost, rates.LocationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
