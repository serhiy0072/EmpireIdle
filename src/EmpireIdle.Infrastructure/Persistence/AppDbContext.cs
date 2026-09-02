using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Auth;
using EmpireIdle.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence
{
    /// <summary>
    /// Головний контекст бази даних. Реалізує Unit of Work через EF Core.
    /// </summary>
    public class AppDbContext : IdentityDbContext
    {
        private readonly IServerContext _serverContext;
        public AppDbContext(DbContextOptions<AppDbContext> options, IServerContext serverContext) : base(options)
            => _serverContext = serverContext;


        public DbSet<Player> Players => Set<Player>();
        public DbSet<Village> Villages => Set<Village>();
        public DbSet<Building> Buildings => Set<Building>();
        public DbSet<PlayerWallet> PlayerWallets => Set<PlayerWallet>();
        public DbSet<VillageResource> VillageResources => Set<VillageResource>();
        public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Garrison> Garrisons => Set<Garrison>();
        public DbSet<MapCell> MapCells => Set<MapCell>();
        public DbSet<Monster> Monsters => Set<Monster>();
        public DbSet<March> Marches => Set<March>();
        public DbSet<BattleReport> BattleReports => Set<BattleReport>();
        public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
        public DbSet<LootBoxProgress> LootBoxProgress => Set<LootBoxProgress>();
        public DbSet<PlayerItem> PlayerItems => Set<PlayerItem>();
        public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
        public DbSet<ActiveEffect> ActiveEffects => Set<ActiveEffect>();
        public DbSet<RecoverableUnit> RecoverableUnits => Set<RecoverableUnit>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<QuestProgress> QuestProgress => Set<QuestProgress>();
        public DbSet<ServerQuestProgress> ServerQuestProgress => Set<ServerQuestProgress>();
        public DbSet<ServerQuestContribution> ServerQuestContributions => Set<ServerQuestContribution>();
        public DbSet<Server> Servers => Set<Server>();
        public DbSet<PlayerPower> PlayerPowers => Set<PlayerPower>();
        public DbSet<PlayerRating> PlayerRatings => Set<PlayerRating>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            // Міжсерверний витік стає структурно неможливим: фільтр застосовується
            // до кожного запиту, забути його не можна.
            modelBuilder.Entity<Player>().HasQueryFilter(p => p.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<Village>().HasQueryFilter(v => v.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<Monster>().HasQueryFilter(m => m.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<MapCell>().HasQueryFilter(c => c.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<March>().HasQueryFilter(m => m.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<ServerQuestProgress>().HasQueryFilter(q => q.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<ServerQuestContribution>().HasQueryFilter(c => c.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<QuestProgress>().HasQueryFilter(q => q.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<Garrison>().HasQueryFilter(g => g.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<PlayerPower>().HasQueryFilter(p => p.ServerId == _serverContext.ServerId);
            modelBuilder.Entity<PlayerRating>().HasQueryFilter(r => r.ServerId == _serverContext.ServerId);
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
                    modelBuilder.Entity(entityType.ClrType).Ignore(nameof(Entity.DomainEvents));
            }
        }
    }
}
