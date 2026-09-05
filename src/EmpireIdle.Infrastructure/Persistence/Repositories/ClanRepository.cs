using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ClanRepository : IClanRepository
    {
        private readonly AppDbContext _context;

        public ClanRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<Clan?> GetByIdAsync(Guid clanId, CancellationToken cancellationToken = default)
            => WithAggregate().FirstOrDefaultAsync(c => c.Id == clanId, cancellationToken);

        /// <inheritdoc/>
        public Task<Clan?> GetByMemberAsync(Guid playerId, CancellationToken cancellationToken = default)
            => WithAggregate().FirstOrDefaultAsync(c => c.Members.Any(m => m.PlayerId == playerId), cancellationToken);

        /// <inheritdoc/>
        public Task<bool> ExistsAsync(string name, string tag, CancellationToken cancellationToken = default)
            => _context.Clans
                .AsNoTracking()
                .AnyAsync(c => c.Name == name || c.Tag == tag, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(Clan clan, CancellationToken cancellationToken = default)
            => await _context.Clans.AddAsync(clan, cancellationToken);

        /// <inheritdoc/>
        public void Remove(Clan clan) => _context.Clans.Remove(clan);

        /// <summary>
        /// Клан із двома колекціями. AsSplitQuery — інакше декартів добуток
        /// складу на ролі: п'ятдесят учасників × шість ролей = триста рядків
        /// замість п'ятдесяти шести.
        /// </summary>
        private IQueryable<Clan> WithAggregate()
            => _context.Clans
                .Include(c => c.Members)
                .Include(c => c.Roles)
                .AsSplitQuery();

        /// <inheritdoc/>
        public async Task<(IReadOnlyList<ClanCard> Items, int Total)> BrowseAsync(string? search, int skip, int take,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Clans.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                // ILike, а не ToLower().Contains(): у Postgres це пошук
                // без регістру на боці бази, назви кланів гравці пишуть як завгодно
                var pattern = $"%{search.Trim()}%";
                query = query.Where(c => EF.Functions.ILike(c.Name, pattern)
                                      || EF.Functions.ILike(c.Tag, pattern));
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(c => c.Members.Count)
                .ThenBy(c => c.Name)
                .Skip(skip)
                .Take(take)
                .Select(c => new ClanCard(c.Id, c.Name, c.Tag, c.Description,
                    c.JoinPolicy, c.Members.Count, c.CreatedAt))
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        /// <inheritdoc/>
        public Task<ClanCard?> GetCardAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.Clans
                .AsNoTracking()
                .Where(c => c.Id == clanId)
                .Select(c => new ClanCard(c.Id, c.Name, c.Tag, c.Description,
                    c.JoinPolicy, c.Members.Count, c.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<Guid?> GetClanIdByMemberAsync(Guid playerId, CancellationToken cancellationToken = default)
            => await _context.Clans
                .AsNoTracking()
                .Where(c => c.Members.Any(m => m.PlayerId == playerId))
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(cancellationToken);
    }
}
