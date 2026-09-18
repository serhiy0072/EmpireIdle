using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Heroes.Queries
{
    /// <summary>
    /// Id дозрілих замовлень на прокачку. Сканерний запит: віддає лише
    /// ідентифікатори, бо кожне далі обробляється у власному scope —
    /// сутність із чужого контексту там не збережеться.
    /// </summary>
    public record GetHeroOrderIdsWithDueLevelUpQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetHeroOrderIdsWithDueLevelUpQueryHandler
        : IRequestHandler<GetHeroOrderIdsWithDueLevelUpQuery, IReadOnlyList<Guid>>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetHeroOrderIdsWithDueLevelUpQueryHandler(
            IHeroRepository heroRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _heroRepository = heroRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetHeroOrderIdsWithDueLevelUpQuery request, CancellationToken cancellationToken)
            => _heroRepository.GetIdsWithDueLevelUpAsync(
                _timeProvider.GetUtcNow().UtcDateTime,
                _catalog.Config.ScanBatchSize,
                cancellationToken);
    }
}
