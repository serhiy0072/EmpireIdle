using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Market.Contracts;
using EmpireIdle.Application.Market.Services;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Market.Queries
{
    /// <summary>Системний запит сканера: лоти, чий строк минув.</summary>
    public record GetMarketListingIdsDueToExpireQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetMarketListingIdsDueToExpireQueryHandler
        : IRequestHandler<GetMarketListingIdsDueToExpireQuery, IReadOnlyList<Guid>>
    {
        private readonly IMarketRepository _market;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetMarketListingIdsDueToExpireQueryHandler(IMarketRepository market, GameCatalog catalog, TimeProvider timeProvider)
        {
            _market = market;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetMarketListingIdsDueToExpireQuery request, CancellationToken cancellationToken)
            => _market.GetIdsDueToExpireAsync(_timeProvider.GetUtcNow().UtcDateTime, _catalog.Config.ScanBatchSize, cancellationToken);
    }
}
