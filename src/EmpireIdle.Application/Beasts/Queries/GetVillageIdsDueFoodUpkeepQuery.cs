using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Beasts.Queries
{
    /// <summary>
    /// Id сіл, чий звіринець пора годувати: востаннє — щонайменше Interval тому (рішення 13.10.2026).
    /// Сканерний запит: лише ідентифікатори, кожне село годується у власному scope.
    /// </summary>
    public record GetVillageIdsDueFoodUpkeepQuery : IRequest<IReadOnlyList<Guid>>
    {
        /// <summary>Як часто звіринець їсть. Збігається з розкладом джоба — частіше рахувати немає сенсу.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    }

    public sealed class GetVillageIdsDueFoodUpkeepQueryHandler
        : IRequestHandler<GetVillageIdsDueFoodUpkeepQuery, IReadOnlyList<Guid>>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetVillageIdsDueFoodUpkeepQueryHandler(IVillageRepository villageRepository, GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetVillageIdsDueFoodUpkeepQuery request, CancellationToken cancellationToken)
        {
            // Звіринець — будівля, що вміщає звірів; ключа в коді немає, як і в місткості
            var penTypes = _catalog.Config.Buildings.Where(b => b.BeastCapacityPerLevel > 0).Select(b => b.Key).ToList();

            // Невелике вікно запасу: джоб, що запустився на секунду раніше, не пропускає село до наступного разу
            var dueBefore = _timeProvider.GetUtcNow().UtcDateTime - GetVillageIdsDueFoodUpkeepQuery.Interval + TimeSpan.FromMinutes(1);

            return _villageRepository.GetIdsDueFoodUpkeepAsync(penTypes, dueBefore, _catalog.Config.ScanBatchSize,
                cancellationToken);
        }
    }
}
