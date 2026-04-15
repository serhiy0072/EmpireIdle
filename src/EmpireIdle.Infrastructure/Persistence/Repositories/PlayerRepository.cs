using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Реалізація репозиторію Player через EF Core.
    /// </summary>
    public class PlayerRepository : IPlayerRepository
    {
        private readonly AppDbContext _context;
        public PlayerRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task AddAsync(Player entity, CancellationToken cancellationToken = default)
            => await _context.Players.AddAsync(entity, cancellationToken);

        /// <inheritdoc/>
        public async Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => await _context.Players.FirstOrDefaultAsync(p => p.Email == email, cancellationToken);

        /// <inheritdoc/>
        public async Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Players.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        /// <inheritdoc/>
        public void Update(Player entity) => _context.Update(entity);

        /// <inheritdoc/>
        public void Delete(Player entity) => _context.Remove(entity);

    }
}
