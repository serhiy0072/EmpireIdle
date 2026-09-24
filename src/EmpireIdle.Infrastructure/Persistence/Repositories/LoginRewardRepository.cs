using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class LoginRewardRepository : ILoginRewardRepository
    {
        private readonly AppDbContext _context;

        public LoginRewardRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<LoginRewardProgress?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.LoginRewardProgress.FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        public async Task AddAsync(LoginRewardProgress progress, CancellationToken cancellationToken = default)
            => await _context.LoginRewardProgress.AddAsync(progress, cancellationToken);
    }
}
