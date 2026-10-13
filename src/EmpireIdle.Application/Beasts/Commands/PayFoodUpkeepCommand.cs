using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Beasts.Commands
{
    /// <summary>
    /// Годує звіринець одного села за час від минулого годування (рішення 13.10.2026). Одиниця роботи
    /// джоба: конфлікт паралелізму коштує це село, а не весь прогін.
    /// </summary>
    public record PayFoodUpkeepCommand(Guid VillageId) : IRequest;

    public sealed class PayFoodUpkeepCommandHandler : IRequestHandler<PayFoodUpkeepCommand>
    {
        /// <summary>Їжа — ресурс, який їдять звірі; так само, як і в активації звіра.</summary>
        private const string FoodResource = "food";

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
        private readonly ILogger<PayFoodUpkeepCommandHandler> _logger;

        public PayFoodUpkeepCommandHandler(
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
            ILogger<PayFoodUpkeepCommandHandler> logger)
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

        public async Task Handle(PayFoodUpkeepCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Село могло зникнути між читанням id і обробкою — не помилка джоба
            var village = await _villageRepository.GetByIdAsync(request.VillageId, cancellationToken);
            if (village is null)
                return;

            var pen = await _beastPens.GetByPlayerReadOnlyAsync(village.PlayerId, cancellationToken);
            var hourly = _catalog.Config.Beasts.FoodUpkeep.HourlyUpkeep(
                _capacities.BeastPenLevel(village, _status),
                pen?.Beasts.Select(b => b.Level) ?? []);

            // Перше годування лише ставить відлік: до нього звіринець нічого не їв
            var since = village.FoodUpkeepAt ?? now;
            var amount = (int)Math.Floor(hourly * Math.Max(0, (now - since).TotalHours));

            // Буфери ферм і голодний темп фіксуються, тож потрібні буст і кільце, як при зборі
            var boost = await _effectResolver.GetProductionBoostAsync(village.PlayerId, now, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);
            var wasHungry = village.IsHungry;

            var missing = village.PayFoodUpkeep(amount, FoodResource,
                _catalog.Config.Beasts.FoodUpkeep.HungerProductionMultiplier, _catalog.Buildings, now, boost,
                locationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (village.IsHungry != wasHungry)
                _logger.LogInformation("Village {VillageId} is {State}: beast pen ate {Amount} food, {Missing} missing",
                    village.Id, village.IsHungry ? "hungry" : "fed again", amount, missing);
        }
    }
}
