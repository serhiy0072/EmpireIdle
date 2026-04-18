using EmpireIdle.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Infrastructure.Persistence
{
    /// <summary>
    /// Реалізація Unit of Work через EF Core.
    /// Зберігає всі зміни одним викликом SaveChangesAsync.
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
