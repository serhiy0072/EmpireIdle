using EmpireIdle.Application.Battles.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using MediatR;

namespace EmpireIdle.Application.Battles.Queries
{
    /// <summary>Останні звіти про бої гравця.</summary>
    public record GetBattleReportsQuery(Guid PlayerId, int Take = 20) : IRequest<List<BattleReportView>>, IPlayerScopedRequest;

    public sealed class GetBattleReportsQueryHandler : IRequestHandler<GetBattleReportsQuery, List<BattleReportView>>
    {
        private const int MaxTake = 50;

        private readonly IBattleReportRepository _repository;

        public GetBattleReportsQueryHandler(IBattleReportRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<BattleReportView>> Handle(GetBattleReportsQuery request, CancellationToken cancellationToken)
        {
            var take = Math.Clamp(request.Take, 1, MaxTake);

            return (await _repository.GetByPlayerAsync(request.PlayerId, take, cancellationToken))
                .Select(r => new BattleReportView(
                    r.Id, r.MarchId, r.X, r.Y, r.TerrainType,
                    r.TargetName, r.TargetLevel, r.Won,
                    r.AttackerPower, r.DefenderPower, r.FoughtAt, r.IsRead,
                    r.Lines
                        .Select(l => new BattleReportLineView(l.UnitType, l.Sent, l.Survived, l.Wounded, l.Recoverable, l.Dead))
                        .ToList(),
                    r.TamedBeastKey))
                .ToList();
        }
    }
}
