using EmpireIdle.Application.Beasts.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Beasts.Queries
{
    /// <summary>
    /// Найближчий до села монстр, з якого виходить звір BeastKey (рішення 21.10.2026): кнопка
    /// «Знайти» у звіринці веде просто до нього. null — такого монстра у світі зараз немає.
    /// Чи вистачить сили — вирішує гравець у формі маршу, як і з будь-якою ціллю.
    /// </summary>
    public record GetNearestTameTargetQuery(Guid PlayerId, string BeastKey) : IRequest<TameTargetView?>, IPlayerScopedRequest;

    public sealed class GetNearestTameTargetQueryHandler : IRequestHandler<GetNearestTameTargetQuery, TameTargetView?>
    {
        private readonly IVillageRepository _villages;
        private readonly IMonsterRepository _monsters;
        private readonly GameCatalog _catalog;

        public GetNearestTameTargetQueryHandler(IVillageRepository villages, IMonsterRepository monsters, GameCatalog catalog)
        {
            _villages = villages;
            _monsters = monsters;
            _catalog = catalog;
        }

        public async Task<TameTargetView?> Handle(GetNearestTameTargetQuery request, CancellationToken cancellationToken)
        {
            // Ключ приходить від клієнта: невідомий — це 404, а не збій каталогу
            if (!_catalog.Beasts.TryGetValue(request.BeastKey, out var beast))
                throw new EntityNotFoundException("Beast", request.BeastKey);

            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var monster = await _monsters.GetNearestAsync(village.ServerId, beast.MonsterKey, village.X, village.Y, cancellationToken);

            return monster is null ? null : new TameTargetView(monster.Id, monster.Type, monster.Level, monster.X, monster.Y);
        }
    }
}
