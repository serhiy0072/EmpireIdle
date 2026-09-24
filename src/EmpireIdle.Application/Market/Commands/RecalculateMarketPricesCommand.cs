using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Market.Commands
{
    /// <summary>
    /// Перераховує знімки медіани поточного світу з продажів за вікно
    /// (GDD §8.8). Системна команда джоба; виставлення читає лише знімок.
    /// </summary>
    public record RecalculateMarketPricesCommand(int ServerId) : IRequest;

    public sealed class RecalculateMarketPricesCommandHandler : IRequestHandler<RecalculateMarketPricesCommand>
    {
        private readonly IMarketRepository _market;
        private readonly MarketPricing _pricing;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RecalculateMarketPricesCommandHandler> _logger;

        public RecalculateMarketPricesCommandHandler(
            IMarketRepository market,
            MarketPricing pricing,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<RecalculateMarketPricesCommandHandler> logger)
        {
            _market = market;
            _pricing = pricing;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RecalculateMarketPricesCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var since = now.AddHours(-_catalog.Config.Market.MedianWindowHours);

            var sales = (await _market.GetSalesSinceAsync(since, cancellationToken))
                .GroupBy(sale => sale.PricingKey)
                .ToDictionary(g => g.Key, g => g.Select(sale => sale.PricePerUnit).ToList());

            var snapshots = (await _market.GetSnapshotsAsync(cancellationToken)).ToDictionary(s => s.PricingKey);

            // Категорії без продажів за вікно теж оновлюються: стара медіана
            // інакше жила б вічно й тримала коридор, якого ринок уже не бачить
            foreach (var key in sales.Keys.Union(snapshots.Keys))
            {
                var prices = sales.GetValueOrDefault(key, []);
                var median = _pricing.Median(prices);

                if (snapshots.TryGetValue(key, out var snapshot))
                    snapshot.Update(median, prices.Count, now);
                else
                    await _market.AddSnapshotAsync(new MarketPriceSnapshot(request.ServerId, key, median, prices.Count, now),
                        cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Market medians recalculated for server {ServerId}: {Categories} categories, {Sales} sales",
                request.ServerId, sales.Count, sales.Values.Sum(p => p.Count));
        }
    }
}
