using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Beasts.Queries
{
    /// <summary>
    /// Id сіл, яким пора сплатити провізію — є Їдальня чи звіринець, а востаннє платили щонайменше
    /// Interval тому (рішення 13.10.2026, 20.10.2026).
    /// Сканерний запит: лише ідентифікатори, кожне село годується у власному scope.
    /// </summary>
    public record GetVillageIdsDueProvisionsQuery : IRequest<IReadOnlyList<Guid>>
    {
        /// <summary>Як часто село й звіринець їдять. Збігається з розкладом джоба — частіше рахувати немає сенсу.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    }

    public sealed class GetVillageIdsDueProvisionsQueryHandler
        : IRequestHandler<GetVillageIdsDueProvisionsQuery, IReadOnlyList<Guid>>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetVillageIdsDueProvisionsQueryHandler(IVillageRepository villageRepository, GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetVillageIdsDueProvisionsQuery request, CancellationToken cancellationToken)
        {
            // Хто їсть: звіринець (будівля, що вміщає звірів; ключа в коді немає, як і в місткості) і Їдальня
            var eaters = _catalog.Config.Buildings.Where(b => b.BeastCapacityPerLevel > 0).Select(b => b.Key).ToList();
            if (_catalog.Config.Provisions.MessHallBuildingKey is { } messHall)
                eaters.Add(messHall);

            // Невелике вікно запасу: джоб, що запустився на секунду раніше, не пропускає село до наступного разу
            var dueBefore = _timeProvider.GetUtcNow().UtcDateTime - GetVillageIdsDueProvisionsQuery.Interval + TimeSpan.FromMinutes(1);

            return _villageRepository.GetIdsDueProvisionsAsync(eaters, dueBefore, _catalog.Config.ScanBatchSize,
                cancellationToken);
        }
    }
}
