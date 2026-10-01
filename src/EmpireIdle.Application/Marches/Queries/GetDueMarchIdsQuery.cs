using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Marches.Queries
{
    /// <summary>
    /// Id походів, чий час настав. Сканерний запит: лише ідентифікатори — кожен похід
    /// далі обробляє ServerJobRunner у власному scope, як і решту таймерів.
    /// </summary>
    public record GetDueMarchIdsQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetDueMarchIdsQueryHandler : IRequestHandler<GetDueMarchIdsQuery, IReadOnlyList<Guid>>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetDueMarchIdsQueryHandler(IMarchRepository marchRepository, GameCatalog catalog, TimeProvider timeProvider)
        {
            _marchRepository = marchRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<Guid>> Handle(GetDueMarchIdsQuery request, CancellationToken cancellationToken)
            => (await _marchRepository.GetDueAsync(
                    _timeProvider.GetUtcNow().UtcDateTime, _catalog.Config.ScanBatchSize, cancellationToken))
                .Select(m => m.Id)
                .ToList();
    }
}
