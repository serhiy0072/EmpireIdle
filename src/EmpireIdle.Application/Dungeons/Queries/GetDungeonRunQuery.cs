using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Dungeons.Queries
{
    /// <summary>
    /// Поточний забіг гравця з розгорнутим станом бою. Потрібен після
    /// перезавантаження сторінки: клієнт відновлює бій із нуля, а не тримає
    /// його в пам'яті вкладки.
    /// </summary>
    public record GetDungeonRunQuery(Guid PlayerId) : IRequest<DungeonRunView?>, IPlayerScopedRequest;

    internal sealed class GetDungeonRunQueryHandler : IRequestHandler<GetDungeonRunQuery, DungeonRunView?>
    {
        private readonly IDungeonRepository _dungeons;
        private readonly BattleBuilder _builder;
        private readonly GameCatalog _catalog;

        public GetDungeonRunQueryHandler(IDungeonRepository dungeons, BattleBuilder builder, GameCatalog catalog)
        {
            _dungeons = dungeons;
            _builder = builder;
            _catalog = catalog;
        }

        public async Task<DungeonRunView?> Handle(GetDungeonRunQuery request, CancellationToken cancellationToken)
        {
            var run = await _dungeons.GetActiveRunAsync(request.PlayerId, cancellationToken);

            if (run is null)
                return null;

            var state = BattleSerializer.Read(run.Battle);

            return DungeonRunProjection.Project(run.Id, run.DungeonKey, run.Level, run.State, state,
                _builder.WaveCount(run.Level), turns: [], reward: null, _catalog);
        }
    }
}
