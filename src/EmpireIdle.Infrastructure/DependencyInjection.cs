using EmpireIdle.Application.Common.Behaviors;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Infrastructure.Auth;
using EmpireIdle.Infrastructure.Payments;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Interceptors;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
            // Database
            services.AddDbContext<AppDbContext>((sp, options) =>
                 options
                    .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                    .AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>()));

            // Unit of work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Repositories
            services.AddScoped<IVillageRepository, VillageRepository>();
            services.AddScoped<IPlayerRepository, PlayerRepository>();
            services.AddScoped<IPlayerWalletRepository, PlayerWalletRepository>();
            services.AddScoped<IGarrisonRepository, GarrisonRepository>();
            services.AddScoped<IMapRepository, MapRepository>();
            services.AddScoped<DomainEventDispatchInterceptor>();
            services.AddScoped<IMonsterRepository, MonsterRepository>();
            services.AddScoped<IMarchRepository, MarchRepository>();
            services.AddScoped<IBattleReportRepository, BattleReportRepository>();
            services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
            services.AddScoped<IInventoryRepository, InventoryRepository>();
            services.AddScoped<ItemGranter>();
            services.AddScoped<IItemEffect, ResourceItemEffect>();
            services.AddScoped<ItemEffectDispatcher>();
            services.AddScoped<IActiveEffectRepository, ActiveEffectRepository>();
            services.AddScoped<IItemEffect, BoostItemEffect>();
            services.AddScoped<EffectResolver>();
            services.Configure<StripeSettings>(configuration.GetSection(nameof(StripeSettings)));
            services.AddScoped<IPaymentProvider, StripePaymentProvider>();


            services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(IRepository<>).Assembly);
                    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                    cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
                    cfg.AddOpenBehavior(typeof(PlayerScopeBehavior<,>));
                    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));

                });
            services.AddValidatorsFromAssembly(typeof(IRepository<>).Assembly);

            // Identity
            services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
                .AddEntityFrameworkStores<AppDbContext>();

            // Auth
            services.AddScoped<AuthService>();

            return services;
        }
    }
}
