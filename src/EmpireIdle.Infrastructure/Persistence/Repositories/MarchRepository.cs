using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.ReadModels;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій походів (EF Core).</summary>
    public class MarchRepository : IMarchRepository
    {
        private readonly AppDbContext _context;

        public MarchRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<March>> GetActiveByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default)
            => _context.Marches
            .Include(m => m.Units)
            .Include(m => m.Cargo)
            .AsSplitQuery()
            .Where(m => m.GarrisonId == garrisonId && m.State != MarchState.Completed)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<March>> GetDueAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
            => _context.Marches
            .AsNoTracking()
            .Where(m => m.State != MarchState.Completed && m.ArrivesAt <= utcNow)
            .OrderBy(m => m.ArrivesAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<March?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Marches
            .Include(m => m.Units)
            .Include(m => m.Cargo)
            .AsSplitQuery()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        /// <inheritdoc/>
        public async Task<List<IncomingAttack>> GetIncomingAttacksAsync(IReadOnlyCollection<Guid> defenderPlayerIds, Guid? clanId,
            CancellationToken cancellationToken = default)
        {
            var ids = defenderPlayerIds.ToList();

            var marches = Hostile().Where(m =>
                (m.TargetType == MarchTargetType.Village
                    && _context.Villages.Any(v => v.Id == m.TargetId && ids.Contains(v.PlayerId)))
                || (clanId != null && m.TargetType == MarchTargetType.ClanStructure
                    && _context.ClanStructures.Any(s => s.Id == m.TargetId && s.ClanId == clanId)));

            // Сортування після проєкції в record EF не перекладає; загроз одиниці — сортуємо в пам'яті
            var attacks = await Project(marches).ToListAsync(cancellationToken);

            return attacks.OrderBy(a => a.ArrivesAt).ToList();
        }

        /// <inheritdoc/>
        public Task<IncomingAttack?> GetIncomingAttackAsync(Guid marchId, CancellationToken cancellationToken = default)
            => Project(Hostile().Where(m => m.Id == marchId)).FirstOrDefaultAsync(cancellationToken);

        /// <summary>
        /// Напади й розвідка в дорозі: на монстрів тривог немає, повернення й підкріплення — не загроза.
        /// Та сама умова, що й March.IsHostileToPlayers, — у SQL-формі.
        /// </summary>
        private IQueryable<March> Hostile()
            => _context.Marches
            .AsNoTracking()
            .Where(m => m.State == MarchState.Outbound
                && m.Intent != MarchIntent.Reinforce
                && m.TargetType != MarchTargetType.Monster);

        /// <summary>
        /// Одна проєкція на все: нападник — через гарнізон-відправник до його села й гравця,
        /// ціль — село (назва, власник, його клан) або споруда (тег і клан-власник).
        /// </summary>
        private IQueryable<IncomingAttack> Project(IQueryable<March> marches)
            => from m in marches
               join g in _context.Garrisons on m.GarrisonId equals g.Id
               join av in _context.Villages on g.HostId equals av.Id
               join ap in _context.Players on av.PlayerId equals ap.Id
               let targetVillage = _context.Villages.FirstOrDefault(v => v.Id == m.TargetId)
               let targetStructure = _context.ClanStructures.FirstOrDefault(s => s.Id == m.TargetId)
               select new IncomingAttack(
                   m.Id,
                   m.Intent,
                   m.TargetType,
                   m.TargetId,
                   m.TargetType == MarchTargetType.Village
                       ? targetVillage!.Name
                       : _context.Clans.Where(c => c.Id == targetStructure!.ClanId).Select(c => c.Tag).FirstOrDefault(),
                   m.TargetType == MarchTargetType.Village ? (Guid?)targetVillage!.PlayerId : null,
                   m.TargetType == MarchTargetType.Village
                       ? _context.Players.Where(p => p.Id == targetVillage!.PlayerId).Select(p => p.ClanId).FirstOrDefault()
                       : (Guid?)targetStructure!.ClanId,
                   m.TargetX,
                   m.TargetY,
                   m.OriginX,
                   m.OriginY,
                   ap.Id,
                   ap.Username,
                   _context.Clans.Where(c => c.Id == ap.ClanId).Select(c => c.Tag).FirstOrDefault(),
                   m.DepartedAt,
                   m.ArrivesAt);

        /// <inheritdoc/>
        public async Task AddAsync(March march, CancellationToken cancellationToken = default)
            =>  await _context.Marches.AddAsync(march, cancellationToken);
        
    }
}

