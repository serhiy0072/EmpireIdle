using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Villages.Queries
{
    /// <summary>
    /// Id сіл, яким пора автозбору: востаннє — щонайменше GameConfig.AutoCollectSeconds тому (рішення 19.10.2026).
    /// Сканерний запит: лише ідентифікатори, кожне село збирається у власному scope.
    /// </summary>
    public record GetVillageIdsDueAutoCollectQuery : IRequest<IReadOnlyList<Guid>>
    {
        /// <summary>Запас: тік, що запустився трохи раніше, не пропускає село до наступного разу.</summary>
        public static readonly TimeSpan EarlyTickMargin = TimeSpan.FromSeconds(5);
    }

    public sealed class GetVillageIdsDueAutoCollectQueryHandler
        : IRequestHandler<GetVillageIdsDueAutoCollectQuery, IReadOnlyList<Guid>>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetVillageIdsDueAutoCollectQueryHandler(IVillageRepository villageRepository, GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetVillageIdsDueAutoCollectQuery request, CancellationToken cancellationToken)
        {
            var dueBefore = _timeProvider.GetUtcNow().UtcDateTime
                - TimeSpan.FromSeconds(_catalog.Config.AutoCollectSeconds)
                + GetVillageIdsDueAutoCollectQuery.EarlyTickMargin;

            return _villageRepository.GetIdsDueAutoCollectAsync(dueBefore, _catalog.Config.ScanBatchSize, cancellationToken);
        }
    }
}
