using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class MarketRepository : IMarketRepository
    {
        private readonly AppDbContext _context;

        public MarketRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<MarketListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.MarketListings.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        public async Task AddAsync(MarketListing listing, CancellationToken cancellationToken = default)
            => await _context.MarketListings.AddAsync(listing, cancellationToken);

        public Task<int> CountActiveAsync(Guid sellerId, CancellationToken cancellationToken = default)
            => _context.MarketListings
                .CountAsync(l => l.SellerId == sellerId && l.State == MarketListingState.Active, cancellationToken);

        public async Task<List<MarketListing>> GetBySellerAsync(Guid sellerId, int closedToShow,
            CancellationToken cancellationToken = default)
        {
            var active = await _context.MarketListings
                .AsNoTracking()
                .Where(l => l.SellerId == sellerId && l.State == MarketListingState.Active)
                .OrderByDescending(l => l.ListedAt)
                .ToListAsync(cancellationToken);

            var closed = await _context.MarketListings
                .AsNoTracking()
                .Where(l => l.SellerId == sellerId && l.State != MarketListingState.Active)
                .OrderByDescending(l => l.ClosedAt)
                .Take(closedToShow)
                .ToListAsync(cancellationToken);

            return [.. active, .. closed];
        }

        public async Task<(List<MarketListing> Page, int Total)> BrowseAsync(MarketListingKind? kind, string? itemKey,
            DateTime utcNow, int skip, int take, CancellationToken cancellationToken = default)
        {
            var query = _context.MarketListings
                .AsNoTracking()
                .Where(l => l.State == MarketListingState.Active && l.ExpiresAt > utcNow);

            if (kind is { } k)
                query = query.Where(l => l.Kind == k);

            if (!string.IsNullOrEmpty(itemKey))
                query = query.Where(l => l.ItemKey == itemKey);

            var total = await query.CountAsync(cancellationToken);

            // Найдешевші за одиницю першими — так покупець бачить вигідне, а не нове
            var page = await query
                .OrderBy(l => l.PriceGold / l.Units)
                .ThenBy(l => l.ListedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (page, total);
        }

        public async Task<IReadOnlyList<Guid>> GetIdsDueToExpireAsync(DateTime utcNow, int batchSize,
            CancellationToken cancellationToken = default)
            => await _context.MarketListings
                .Where(l => l.State == MarketListingState.Active && l.ExpiresAt <= utcNow)
                .OrderBy(l => l.ExpiresAt)
                .Select(l => l.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

        public async Task<List<(string PricingKey, double PricePerUnit)>> GetSalesSinceAsync(DateTime since,
            CancellationToken cancellationToken = default)
        {
            var rows = await _context.MarketListings
                .AsNoTracking()
                .Where(l => l.State == MarketListingState.Sold && l.ClosedAt >= since)
                .Select(l => new { l.PricingKey, PerUnit = l.PriceGold / l.Units })
                .ToListAsync(cancellationToken);

            return rows.Select(r => (r.PricingKey, r.PerUnit)).ToList();
        }

        public Task<MarketPriceSnapshot?> GetSnapshotAsync(string pricingKey, CancellationToken cancellationToken = default)
            => _context.MarketPriceSnapshots.AsNoTracking().FirstOrDefaultAsync(s => s.PricingKey == pricingKey, cancellationToken);

        public Task<List<MarketPriceSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
            => _context.MarketPriceSnapshots.ToListAsync(cancellationToken);

        public async Task AddSnapshotAsync(MarketPriceSnapshot snapshot, CancellationToken cancellationToken = default)
            => await _context.MarketPriceSnapshots.AddAsync(snapshot, cancellationToken);
    }
}
