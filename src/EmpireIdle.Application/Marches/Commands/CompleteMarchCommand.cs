using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>
    /// Завершує похід, час якого настав. Викликається сканером таймерів.
    /// </summary>
    public record CompleteMarchCommand(Guid MarchId) : IRequest;

    /// <summary>
    /// Обробник CompleteMarchCommand: вирішує, який зі сценаріїв прибуття
    /// застосувати, і сам виконує лише повернення армії додому.
    ///
    /// Сама механіка живе в сервісах під Marches/Services: бій із монстром,
    /// бій за село й доставка підкріплення не перетинаються нічим, окрім
    /// логістики, і тримати їх в одному класі означало б двадцять із
    /// гаком залежностей, з яких кожен сценарій використовує третину.
    /// </summary>
    public sealed class CompleteMarchCommandHandler : IRequestHandler<CompleteMarchCommand>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TerrainGenerator _terrain;
        private readonly TimeProvider _timeProvider;
        private readonly MarchLogistics _logistics;
        private readonly MonsterBattleService _monsterBattle;
        private readonly VillageBattleService _villageBattle;
        private readonly ReinforcementDelivery _reinforcements;
        private readonly ILogger<CompleteMarchCommandHandler> _logger;

        public CompleteMarchCommandHandler(
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            TerrainGenerator terrain,
            TimeProvider timeProvider,
            MarchLogistics logistics,
            MonsterBattleService monsterBattle,
            VillageBattleService villageBattle,
            ReinforcementDelivery reinforcements,
            ILogger<CompleteMarchCommandHandler> logger)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _terrain = terrain;
            _timeProvider = timeProvider;
            _logistics = logistics;
            _monsterBattle = monsterBattle;
            _villageBattle = villageBattle;
            _reinforcements = reinforcements;
            _logger = logger;
        }

        public async Task Handle(CompleteMarchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var march = await _marchRepository.GetByIdAsync(request.MarchId, cancellationToken);

            // Марш міг обробити паралельний прогін сканера
            if (march is null || march.State == MarchState.Completed)
                return;

            if (march.State == MarchState.Outbound)
                await ResolveArrivalAsync(march, now, cancellationToken);
            else if (march.State == MarchState.Returning)
                await ComeHomeAsync(march, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Прибуття до цілі. Намір вирішує, що станеться: підкріплення
        /// лишається в чужому гарнізоні, атака доходить до бою.
        /// </summary>
        private async Task ResolveArrivalAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            if (march.Intent == MarchIntent.Reinforce)
            {
                await _reinforcements.DeliverAsync(march, utcNow, cancellationToken);
                return;
            }

            var attackerArmy = march.GetUnits();
            var terrain = _terrain.GetTerrainType(march.ServerId, march.TargetX, march.TargetY);

            if (march.TargetType == MarchTargetType.Village)
                await _villageBattle.ResolveAsync(march, attackerArmy, terrain, utcNow, cancellationToken);
            else
                await _monsterBattle.ResolveAsync(march, attackerArmy, terrain, utcNow, cancellationToken);
        }

        /// <summary>
        /// Армія вдома: юніти повертаються в гарнізон, здобич — на склад.
        /// Спільне для всіх сценаріїв, тому й лишилось у хендлері.
        /// </summary>
        private async Task ComeHomeAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Garrison {march.GarrisonId} not found for march {march.Id}.");

            var survivors = march.GetUnits();

            if (survivors.Count > 0)
                garrison.ReceiveUnits(survivors, utcNow);

            await _logistics.UnloadCargoAsync(march, garrison, utcNow, cancellationToken);

            march.Complete(utcNow);

            _logger.LogInformation("March {MarchId} returned home with {Count} units.",
                march.Id, survivors.Values.Sum());
        }
    }
}
