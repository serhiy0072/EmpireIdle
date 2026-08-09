using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence
{
    /// <summary>
    /// Головний контекст бази даних. Реалізує Unit of Work через EF Core.
    /// </summary>
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            foreach(var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
                    modelBuilder.Entity(entityType.ClrType).Ignore(nameof(Entity.DomainEvents));
            }
        }
    }
}
