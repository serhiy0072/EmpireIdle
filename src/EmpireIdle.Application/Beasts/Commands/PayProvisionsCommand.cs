using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Beasts.Commands
{
    /// <summary>
    /// Сплачує провізію одного села за час від минулої оплати (рішення 13.10.2026, 20.10.2026): Їдальня
    /// їсть м'ясо, звіринець — корм. Одиниця роботи джоба: конфлікт паралелізму коштує це село, а не весь прогін.
    /// </summary>
    public record PayProvisionsCommand(Guid VillageId) : IRequest;

    public sealed class PayProvisionsCommandHandler : IRequestHandler<PayProvisionsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IBeastPenRepository _beastPens;
        private readonly IServerRepository _serverRepository;
        private readonly EffectResolver _effectResolver;
        private readonly VillageCapacities _capacities;
        private readonly VillageStatus _status;
        private readonly WorldGeometry _geometry;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PayProvisionsCommandHandler> _logger;

        public PayProvisionsCommandHandler(
            IVillageRepository villageRepository,
            IBeastPenRepository beastPens,
            IServerRepository serverRepository,
            EffectResolver effectResolver,
            VillageCapacities capacities,
            VillageStatus status,
            WorldGeometry geometry,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<PayProvisionsCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _beastPens = beastPens;
            _serverRepository = serverRepository;
            _effectResolver = effectResolver;
            _capacities = capacities;
            _status = status;
            _geometry = geometry;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(PayProvisionsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Село могло зникнути між читанням id і обробкою — не помилка джоба
            var village = await _villageRepository.GetByIdAsync(request.VillageId, cancellationToken);
            if (village is null)
                return;

            var pen = await _beastPens.GetByPlayerReadOnlyAsync(village.PlayerId, cancellationToken);
            var fodderPerHour = _catalog.Config.Beasts.FodderUpkeep.HourlyUpkeep(
                _capacities.BeastPenLevel(village, _status),
                pen?.Beasts.Select(b => b.Level) ?? []);
            var mealPerHour = _status.MealPerHour(village);

            // Перша оплата лише ставить відлік: до неї ні Їдальня, ні звіринець нічого не їли
            var paidUntil = village.FoodUpkeepAt ?? now;

            // Буфери й голодний темп фіксуються, тож потрібні буст і кільце, як при зборі
            var boost = await _effectResolver.GetProductionBoostAsync(village.PlayerId, now, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);
            var (wasHungry, beastsWereHungry) = (village.IsHungry, village.BeastsHungry);

            var provisions = _catalog.Config.Provisions;
            var missingMeal = village.PayFoodUpkeep(UpkeepMeter.Due(mealPerHour, paidUntil, now), provisions.MealResource,
                provisions.HungerProductionMultiplier, _catalog.Buildings, now, boost, locationMultiplier);
            var missingFodder = village.PayBeastUpkeep(UpkeepMeter.Due(fodderPerHour, paidUntil, now),
                _catalog.Config.Beasts.UpkeepResource, _catalog.Buildings, now, boost, locationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (village.IsHungry != wasHungry || village.BeastsHungry != beastsWereHungry)
                _logger.LogInformation(
                    "Village {VillageId}: hungry {Hungry} ({MissingMeal} meat missing), beasts hungry {BeastsHungry} ({MissingFodder} fodder missing)",
                    village.Id, village.IsHungry, missingMeal, village.BeastsHungry, missingFodder);
        }
    }
}
