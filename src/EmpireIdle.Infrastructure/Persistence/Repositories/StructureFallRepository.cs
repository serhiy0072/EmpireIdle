using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class StructureFallRepository : IStructureFallRepository
    {
        private readonly AppDbContext _context;

        public StructureFallRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(StructureFall fall, CancellationToken cancellationToken = default)
            => await _context.StructureFalls.AddAsync(fall, cancellationToken);

        public Task<StructureFall?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.StructureFalls.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }
}
