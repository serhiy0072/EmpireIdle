using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using MediatR;

namespace EmpireIdle.Application.Battles.Queries
{
    /// <summary>Останні звіти про бої гравця.</summary>
    public record GetBattleReportsQuery(Guid PlayerId, int Take = 20) : IRequest<List<BattleReport>>, IPlayerScopedRequest;

    public class GetBattleReportsQueryHandler : IRequestHandler<GetBattleReportsQuery, List<BattleReport>>
    {
        private const int MaxTake = 50;

        private readonly IBattleReportRepository _repository;

        public GetBattleReportsQueryHandler(IBattleReportRepository repository)
        {
            _repository = repository;
        }

        public Task<List<BattleReport>> Handle(GetBattleReportsQuery request, CancellationToken cancellationToken)
        {
            var take = Math.Clamp(request.Take, 1, MaxTake);
            return _repository.GetByPlayerAsync(request.PlayerId, take, cancellationToken);
        }
    }
}
