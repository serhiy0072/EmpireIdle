using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Research.Queries
{
    /// <summary>Id записів із дозрілим дослідженням — для сканера таймерів, кожен обробляється у власному scope.</summary>
    public record GetResearchIdsWithDueResearchQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetResearchIdsWithDueResearchQueryHandler
        : IRequestHandler<GetResearchIdsWithDueResearchQuery, IReadOnlyList<Guid>>
    {
        private readonly IPlayerResearchRepository _research;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetResearchIdsWithDueResearchQueryHandler(IPlayerResearchRepository research, GameCatalog catalog, TimeProvider timeProvider)
        {
            _research = research;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetResearchIdsWithDueResearchQuery request, CancellationToken cancellationToken)
            => _research.GetIdsWithDueResearchAsync(_timeProvider.GetUtcNow().UtcDateTime, _catalog.Config.ScanBatchSize, cancellationToken);
    }
}
