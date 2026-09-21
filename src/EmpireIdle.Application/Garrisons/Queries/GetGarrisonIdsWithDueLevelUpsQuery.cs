using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Garrisons.Queries
{
    /// <summary>
    /// Id гарнізонів, у яких є завершені прокачки. Сканерний запит:
    /// віддає лише ідентифікатори, бо кожен гарнізон далі обробляється
    /// у власному scope — сутність із чужого контексту там не збережеться.
    /// </summary>
    public record GetGarrisonIdsWithDueLevelUpsQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetGarrisonIdsWithDueLevelUpsQueryHandler
        : IRequestHandler<GetGarrisonIdsWithDueLevelUpsQuery, IReadOnlyList<Guid>>
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetGarrisonIdsWithDueLevelUpsQueryHandler(
            IGarrisonRepository garrisonRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _garrisonRepository = garrisonRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetGarrisonIdsWithDueLevelUpsQuery request, CancellationToken cancellationToken)
            => _garrisonRepository.GetIdsWithDueLevelUpsAsync(
                _timeProvider.GetUtcNow().UtcDateTime,
                _catalog.Config.ScanBatchSize,
                cancellationToken);
    }
}
