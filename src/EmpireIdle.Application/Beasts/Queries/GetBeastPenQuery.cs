using EmpireIdle.Application.Beasts.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Beasts.Queries
{
    /// <summary>Звіринець гравця. Без рядка в БД — порожній звіринець, а не 404: гравець ще нікого не приручав.</summary>
    public record GetBeastPenQuery(Guid PlayerId) : IRequest<BeastPenView>, IPlayerScopedRequest;

    public sealed class GetBeastPenQueryHandler : IRequestHandler<GetBeastPenQuery, BeastPenView>
    {
        private readonly IBeastPenRepository _pens;
        private readonly IVillageRepository _villages;
        private readonly BeastTaming _taming;
        private readonly BeastProgression _progression;
        private readonly VillageCapacities _capacities;
        private readonly VillageStatus _status;
        private readonly GameCatalog _catalog;

        public GetBeastPenQueryHandler(IBeastPenRepository pens, IVillageRepository villages, BeastTaming taming,
            BeastProgression progression, VillageCapacities capacities, VillageStatus status, GameCatalog catalog)
        {
            _pens = pens;
            _villages = villages;
            _taming = taming;
            _progression = progression;
            _capacities = capacities;
            _status = status;
            _catalog = catalog;
        }

        public async Task<BeastPenView> Handle(GetBeastPenQuery request, CancellationToken cancellationToken)
        {
            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var pen = await _pens.GetByPlayerReadOnlyAsync(request.PlayerId, cancellationToken);
            var penLevel = _capacities.BeastPenLevel(village, _status);
            var pityWins = _catalog.Config.Beasts.PityWins;

            return new BeastPenView(
                _capacities.BeastSlots(village, _status),
                pen?.Beasts
                    .Select(b => new BeastView(b.BeastKey, b.Rank, b.Level, b.Experience,
                        _progression.ExperienceToNext(b.Level), _progression.MaxLevel(b.Rank), b.TamedAt,
                        Passive(b)))
                    .ToList() ?? [],
                _catalog.Config.Beasts.Types
                    .Select(beast => new BeastTamingView(beast.Key, beast.MonsterKey,
                        _taming.ChanceFor(beast, penLevel), pen?.MissesFor(beast.Key) ?? 0, pityWins))
                    .ToList());
        }

        private BeastPassiveView Passive(Domain.Entities.Beast beast)
        {
            var config = _catalog.Beasts[beast.BeastKey];

            // Сума кроків рівня в double дає хвіст (0.21800000000000003) — клієнту віддаємо чисте число
            return new BeastPassiveView(config.Effect.ToString(), Math.Round(_progression.Bonus(config, beast.Level), 6),
                config.DurationMinutes, config.CooldownMinutes, config.ActivationFood, beast.ActiveUntil, beast.CooldownUntil);
        }
    }
}
