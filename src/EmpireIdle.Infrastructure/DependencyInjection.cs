using EmpireIdle.Application.Common.Behaviors;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Application.Quests.Tracking.Mappers;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Granters;
using EmpireIdle.Domain.Events;
using EmpireIdle.Infrastructure.Auth;
using EmpireIdle.Infrastructure.Payments;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Interceptors;
using EmpireIdle.Infrastructure.Persistence.Outbox;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Infrastructure
{
    /// <summary>
    /// Реєстрація всіх Infrastructure залежностей в DI контейнері.
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
            services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<IMapRepository, MapRepository>();
            services.AddScoped<IMonsterRepository, MonsterRepository>();
            services.AddScoped<IMarchRepository, MarchRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerWalletRepository, PlayerWalletRepository>();
            services.AddScoped<IQuestRepository, QuestRepository>();

            // Ефекти предметів — реєструються всі, диспетчер обирає за ключем
            services.AddScoped<IItemEffect, ResourceItemEffect>();
            services.AddScoped<IItemEffect, BoostItemEffect>();
            services.AddScoped<ItemEffectDispatcher>();
            services.AddScoped<EffectResolver>();
            services.AddScoped<ItemGranter>();

            // Нагороди — той самий патерн: усі реалізації + диспетчер за типом
            services.AddScoped<IRewardGranter, GemRewardGranter>();
            services.AddScoped<IRewardGranter, ResourceRewardGranter>();
            services.AddScoped<IRewardGranter, ItemRewardGranter>();
            services.AddScoped<RewardDispatcher>();

            // Квести
            services.AddScoped<QuestSignalResolver>();
            services.AddScoped<QuestProgressTracker>();

            // Мапери подій 
            services.AddScoped<IQuestSignalMapper, BuildingUpgradeCompletedMapper>();
            services.AddScoped<IQuestSignalMapper, BuildingCollectedMapper>();
            services.AddScoped<IQuestSignalMapper, MonsterDefeatedMapper>();
            services.AddScoped<IQuestSignalMapper, BattleFoughtMapper>();
            services.AddScoped<IQuestSignalMapper, GemsSpentMapper>();
            services.AddScoped<IQuestSignalMapper, UnitsTrainedMapper>();

            // Закриті типи хендлера — MediatR не вміє резолвити відкритий генерик
            // для вкладеної нотифікації, тому реєструємо їх рефлексією
            var eventTypes = typeof(IDomainEvent).Assembly
                .GetTypes()
                .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

            foreach (var eventType in eventTypes)
            {
                var notification = typeof(DomainEventNotification<>).MakeGenericType(eventType);
                var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(notification);
                var handlerImplementation = typeof(QuestProgressHandler<>).MakeGenericType(eventType);

                services.AddScoped(handlerInterface, handlerImplementation);
            }

            // Зовнішні сервіси
            services.AddScoped<IPaymentProvider, StripePaymentProvider>();

            services.AddScoped<DomainEventDispatchInterceptor>();

            services.Configure<StripeSettings>(configuration.GetSection(nameof(StripeSettings)));

            services.Configure<OutboxSettings>(configuration.GetSection("Outbox"));
            services.AddHostedService<OutboxProcessor>();

            services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(IRepository<>).Assembly);
                    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
                    cfg.AddOpenBehavior(typeof(PlayerScopeBehavior<,>));
                    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                });
            services.AddValidatorsFromAssembly(typeof(IRepository<>).Assembly);

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
