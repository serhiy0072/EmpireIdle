using EmpireIdle.Application.Battles.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Battles.Queries
{
    /// <summary>Останні звіти про бої гравця. Монстра звіт називає мовою запиту, а не мовою на момент бою.</summary>
    public record GetBattleReportsQuery(Guid PlayerId, int Take = 20) : IRequest<List<BattleReportView>>, IPlayerScopedRequest;

    public sealed class GetBattleReportsQueryHandler : IRequestHandler<GetBattleReportsQuery, List<BattleReportView>>
    {
        private const int MaxTake = 50;

        private readonly IBattleReportRepository _repository;
        private readonly GameCatalog _catalog;
        private readonly LocaleTexts _texts;

        public GetBattleReportsQueryHandler(IBattleReportRepository repository, GameCatalog catalog, LocaleTexts texts)
        {
            _repository = repository;
            _catalog = catalog;
            _texts = texts;
        }

        public async Task<List<BattleReportView>> Handle(GetBattleReportsQuery request, CancellationToken cancellationToken)
        {
            var take = Math.Clamp(request.Take, 1, MaxTake);

            return (await _repository.GetByPlayerAsync(request.PlayerId, take, cancellationToken))
                .Select(r => new BattleReportView(
                    r.Id, r.MarchId, r.X, r.Y, r.TerrainType,
                    TargetName(r), r.TargetLevel, r.Won,
                    r.AttackerPower, r.DefenderPower, r.FoughtAt, r.IsRead,
                    r.Lines
                        .Select(l => new BattleReportLineView(l.UnitType, l.Sent, l.Survived, l.Wounded, l.Recoverable, l.Dead))
                        .ToList(),
                    r.TamedBeastKey,
                    r.HeroExperience))
                .ToList();
        }

        // Монстр із конфігу — мовою читача; село чи клан — власна назва гравця, її не перекладають
        private string TargetName(BattleReport report)
            => report.MonsterType is { } type
                ? _texts.Name("monster", type, _catalog.Monsters.GetValueOrDefault(type)?.DisplayName ?? report.TargetName)
                : report.TargetName;
    }
}
