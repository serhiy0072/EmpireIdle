using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Scouting.ReadModels;
using MediatR;

namespace EmpireIdle.Application.Scouting.Queries
{
    /// <summary>Останні звіти розвідки гравця, новіші першими.</summary>
    public record GetScoutReportsQuery(Guid PlayerId) : IRequest<List<ScoutReportView>>, IPlayerScopedRequest;

    public sealed class GetScoutReportsQueryHandler : IRequestHandler<GetScoutReportsQuery, List<ScoutReportView>>
    {
        /// <summary>Розвідка — знімок моменту: старі звіти застарівають і в списку лише заважають.</summary>
        private const int Recent = 30;

        private readonly IScoutReportRepository _reports;

        public GetScoutReportsQueryHandler(IScoutReportRepository reports) => _reports = reports;

        public async Task<List<ScoutReportView>> Handle(GetScoutReportsQuery request, CancellationToken cancellationToken)
        {
            var reports = await _reports.GetRecentAsync(request.PlayerId, Recent, cancellationToken);

            return reports
                .Select(r => new ScoutReportView(r.Id, r.TargetType, r.TargetId, r.TargetName, r.X, r.Y,
                    r.Outcome.ToString(), r.DefencePower,
                    r.Resources.ToDictionary(x => x.ResourceType, x => x.Amount), r.CreatedAt))
                .ToList();
        }
    }
}
