using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Реалізація репозиторію Payment через EF Core.</summary>
    public class PaymentRepository : IPaymentRepository
    {
        private readonly AppDbContext _context;

        public PaymentRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public async Task AddAsync(Payment entity, CancellationToken cancellationToken = default)
            => await _context.Payments.AddAsync(entity, cancellationToken);

        /// <inheritdoc/>
        public Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<Payment?> GetBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
            => _context.Payments.FirstOrDefaultAsync(p => p.SessionId == sessionId, cancellationToken);
    }
}