using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Лоти ринку й знімки медіани. Усе — у межах поточного світу (query-фільтр).</summary>
    public interface IMarketRepository
    {
        Task<MarketListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task AddAsync(MarketListing listing, CancellationToken cancellationToken = default);

        /// <summary>Скільки активних лотів у продавця — для ліміту від рівня ринку.</summary>
        Task<int> CountActiveAsync(Guid sellerId, CancellationToken cancellationToken = default);

        /// <summary>Активні лоти продавця й кілька останніх закритих, найновіші першими.</summary>
        Task<List<MarketListing>> GetBySellerAsync(Guid sellerId, int closedToShow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Сторінка активних лотів вітрини, найдешевші за одиницю першими.
        /// Лоти, чий строк минув, але сканер їх ще не закрив, не показуються.
        /// </summary>
        Task<(List<MarketListing> Page, int Total)> BrowseAsync(MarketListingKind? kind, string? itemKey, DateTime utcNow,
            int skip, int take, CancellationToken cancellationToken = default);

        /// <summary>Ідентифікатори активних лотів, чий строк минув.</summary>
        Task<IReadOnlyList<Guid>> GetIdsDueToExpireAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>Ціни за одиницю всіх продажів після заданого моменту, за категоріями.</summary>
        Task<List<(string PricingKey, double PricePerUnit)>> GetSalesSinceAsync(DateTime since, CancellationToken cancellationToken = default);

        Task<MarketPriceSnapshot?> GetSnapshotAsync(string pricingKey, CancellationToken cancellationToken = default);

        Task<List<MarketPriceSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);

        Task AddSnapshotAsync(MarketPriceSnapshot snapshot, CancellationToken cancellationToken = default);
    }
}
