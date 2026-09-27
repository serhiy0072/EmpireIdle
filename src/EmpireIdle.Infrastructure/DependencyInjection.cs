using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure.Auth;
using EmpireIdle.Infrastructure.Payments;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Interceptors;
using EmpireIdle.Infrastructure.Persistence.Outbox;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using EmpireIdle.Infrastructure.Translation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Infrastructure
{
    /// <summary>
    /// Реєстрація інфраструктури: БД, репозиторії, outbox, Identity, зовнішні сервіси.
    /// Сценарії застосунку й MediatR — у AddApplication, доменні сервіси — у Program.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Один шлях створення контексту для всіх: DI-інжекція, фонові scope,
            // окремі транзакції. EF Core ≥6: AddDbContextFactory реєструє
            // і сам AppDbContext як scoped — окремий AddDbContext не потрібен.
            services.AddDbContextFactory<AppDbContext>((sp, options) =>
                options
                    .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
                    .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                    .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()),
                ServiceLifetime.Scoped);

            // Unit of work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Repositories
            services.AddScoped<IActiveEffectRepository, ActiveEffectRepository>();
            services.AddScoped<IBattleReportRepository, BattleReportRepository>();
            services.AddScoped<IVillageRepository, VillageRepository>();
            services.AddScoped<IGarrisonRepository, GarrisonRepository>();
            services.AddScoped<IHeroRepository, HeroRepository>();
            services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<IMapRepository, MapRepository>();
            services.AddScoped<IMonsterRepository, MonsterRepository>();
            services.AddScoped<IMarchRepository, MarchRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerWalletRepository, PlayerWalletRepository>();
            services.AddScoped<IQuestRepository, QuestRepository>();
            services.AddScoped<IServerRepository, ServerRepository>();
            services.AddScoped<IPlayerPowerRepository, PlayerPowerRepository>();
            services.AddScoped<ITutorialProgressRepository, TutorialProgressRepository>();
            services.AddScoped<IPlayerRatingRepository, PlayerRatingRepository>();
            services.AddScoped<IServerQuestRepository, ServerQuestRepository>();
            services.AddScoped<IClanRepository, ClanRepository>();
            services.AddScoped<IClanHelpRepository, ClanHelpRepository>();
            services.AddScoped<IClanStructureRepository, ClanStructureRepository>();
            services.AddScoped<IClanQuestRepository, ClanQuestRepository>();
            services.AddScoped<IClanRequestRepository, ClanRequestRepository>();
            services.AddScoped<IBannerRepository, BannerRepository>();
            services.AddScoped<IDungeonRepository, DungeonRepository>();
            services.AddScoped<IMarketRepository, MarketRepository>();
            services.AddScoped<IChatRepository, ChatRepository>();
            services.AddScoped<IMailRepository, MailRepository>();
            services.AddScoped<IVillageFallRepository, VillageFallRepository>();
            services.AddScoped<IStructureFallRepository, StructureFallRepository>();
            services.AddScoped<IScoutReportRepository, ScoutReportRepository>();
            services.AddScoped<ILoginRewardRepository, LoginRewardRepository>();
            services.AddSingleton<ITranslator, NoTranslator>();

            // Зовнішні сервіси
            services.AddSingleton<IRandomSource, SystemRandomSource>();
            services.AddScoped<IPaymentProvider, StripePaymentProvider>();

            services.AddScoped<DomainEventDispatchInterceptor>();

            services.Configure<StripeSettings>(configuration.GetSection(nameof(StripeSettings)));

            services.Configure<OutboxSettings>(configuration.GetSection("Outbox"));
            services.AddHostedService<OutboxProcessor>();

            // Identity
            services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
            })
                .AddEntityFrameworkStores<AppDbContext>();

            // Auth
            services.AddScoped<AuthService>();

            return services;
        }
    }
}
