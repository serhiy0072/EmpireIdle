using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Scouting.Services;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Marches.Commands
{
    /// <summary>
    /// Завершує похід, час якого настав. Викликається сканером таймерів.
    /// </summary>
    public record CompleteMarchCommand(Guid MarchId) : IRequest;

    /// <summary>
    /// Обробник CompleteMarchCommand: вирішує, який зі сценаріїв прибуття
    /// застосувати, і сам лише передає армію, що повернулась, у MarchHomecoming.
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
        private readonly MarchHomecoming _homecoming;
        private readonly MonsterBattleService _monsterBattle;
        private readonly VillageBattleService _villageBattle;
        private readonly ReinforcementDelivery _reinforcements;
        private readonly StructureReinforcementDelivery _structureDelivery;
        private readonly StructureBattleService _structureBattle;
        private readonly ScoutService _scouts;

        public CompleteMarchCommandHandler(
            IMarchRepository marchRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            TerrainGenerator terrain,
            TimeProvider timeProvider,
            MarchHomecoming homecoming,
            MonsterBattleService monsterBattle,
            VillageBattleService villageBattle,
            ReinforcementDelivery reinforcements,
            StructureReinforcementDelivery structureDelivery,
            StructureBattleService structureBattle,
            ScoutService scouts)
        {
            _marchRepository = marchRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _terrain = terrain;
            _timeProvider = timeProvider;
            _homecoming = homecoming;
            _monsterBattle = monsterBattle;
            _villageBattle = villageBattle;
            _reinforcements = reinforcements;
            _structureDelivery = structureDelivery;
            _structureBattle = structureBattle;
            _scouts = scouts;
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
                if (march.TargetType == MarchTargetType.ClanStructure)
                    await _structureDelivery.DeliverAsync(march, utcNow, cancellationToken);
                else
                    await _reinforcements.DeliverAsync(march, utcNow, cancellationToken);

                return;
            }

            var terrain = _terrain.GetTerrainType(march.ServerId, march.TargetX, march.TargetY);

            // Розвідники не б'ються: дивляться й одразу звітують
            if (march.Intent == MarchIntent.Scout)
            {
                await _scouts.ResolveAsync(march, terrain, utcNow, cancellationToken);
                return;
            }

            var attackerArmy = march.GetUnits();

            switch (march.TargetType)
            {
                case MarchTargetType.Village:
                    await _villageBattle.ResolveAsync(march, attackerArmy, terrain, utcNow, cancellationToken);
                    break;
                case MarchTargetType.ClanStructure:
                    await _structureBattle.ResolveAsync(march, attackerArmy, terrain, utcNow, cancellationToken);
                    break;
                default:
                    await _monsterBattle.ResolveAsync(march, attackerArmy, terrain, utcNow, cancellationToken);
                    break;
            }
        }

        /// <summary>Армія вдома: гарнізон відправника приймає її через спільний шлях повернення.</summary>
        private async Task ComeHomeAsync(March march, DateTime utcNow, CancellationToken cancellationToken)
        {
            var garrison = await _garrisonRepository.GetByIdAsync(march.GarrisonId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Garrison {march.GarrisonId} not found for march {march.Id}.");

            await _homecoming.ArriveAsync(march, garrison, leaderSlotFree: null, utcNow, cancellationToken);
        }
    }
}
