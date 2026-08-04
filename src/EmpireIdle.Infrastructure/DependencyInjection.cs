using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Common.Behaviors;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using EmpireIdle.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EmpireIdle.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using FluentValidation;
using MediatR;

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

            services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(IRepository<>).Assembly);
                    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
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
