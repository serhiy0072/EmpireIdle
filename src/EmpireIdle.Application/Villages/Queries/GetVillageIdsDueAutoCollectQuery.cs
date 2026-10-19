using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Villages.Queries
{
    /// <summary>
    /// Id сіл, яким пора автозбору: востаннє — щонайменше Interval тому (рішення 19.10.2026).
    /// Сканерний запит: лише ідентифікатори, кожне село збирається у власному scope.
    /// </summary>
    public record GetVillageIdsDueAutoCollectQuery : IRequest<IReadOnlyList<Guid>>
    {
        /// <summary>Як часто вироблене переходить на склад. Збігається з тіком timer-scan.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
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
            // Запас у кілька секунд: тік, що запустився трохи раніше, не пропускає село до наступного разу
            var dueBefore = _timeProvider.GetUtcNow().UtcDateTime - GetVillageIdsDueAutoCollectQuery.Interval + TimeSpan.FromSeconds(5);

            return _villageRepository.GetIdsDueAutoCollectAsync(dueBefore, _catalog.Config.ScanBatchSize, cancellationToken);
        }
    }
}
