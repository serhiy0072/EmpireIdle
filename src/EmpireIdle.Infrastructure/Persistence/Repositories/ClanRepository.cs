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
    }
}
