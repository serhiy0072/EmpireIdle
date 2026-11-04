using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ClanOfferingRepository : IClanOfferingRepository
    {
        private readonly AppDbContext _context;

        public ClanOfferingRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ClanOfferingLedger?> GetLedgerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.ClanOfferingLedgers.FirstOrDefaultAsync(l => l.PlayerId == playerId, cancellationToken);

        public async Task AddLedgerAsync(ClanOfferingLedger ledger, CancellationToken cancellationToken = default)
            => await _context.ClanOfferingLedgers.AddAsync(ledger, cancellationToken);

        public Task<ClanContribution?> GetContributionAsync(Guid clanId, Guid playerId, CancellationToken cancellationToken = default)
            => _context.ClanContributions.FirstOrDefaultAsync(c => c.ClanId == clanId && c.PlayerId == playerId, cancellationToken);

        public async Task AddContributionAsync(ClanContribution contribution, CancellationToken cancellationToken = default)
            => await _context.ClanContributions.AddAsync(contribution, cancellationToken);

        public Task<int> RemoveContributionsByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanContributions.Where(c => c.ClanId == clanId).ExecuteDeleteAsync(cancellationToken);
    }
}
