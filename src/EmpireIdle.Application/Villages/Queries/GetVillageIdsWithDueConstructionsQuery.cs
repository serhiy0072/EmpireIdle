using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Villages.Queries
{
    /// <summary>
    /// Id сіл, у яких є завершені будівництва. Сканерний запит:
    /// віддає лише ідентифікатори, бо кожне село далі обробляється
    /// у власному scope — сутність із чужого контексту там не збережеться.
    /// </summary>
    public record GetVillageIdsWithDueConstructionsQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetVillageIdsWithDueConstructionsQueryHandler
        : IRequestHandler<GetVillageIdsWithDueConstructionsQuery, IReadOnlyList<Guid>>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetVillageIdsWithDueConstructionsQueryHandler(
            IVillageRepository villageRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetVillageIdsWithDueConstructionsQuery request, CancellationToken cancellationToken)
            => _villageRepository.GetIdsWithDueConstructionsAsync(
                _timeProvider.GetUtcNow().UtcDateTime,
                _catalog.Config.ScanBatchSize,
                cancellationToken);
    }
}
