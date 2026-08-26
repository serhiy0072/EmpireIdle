using EmpireIdle.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>Піднімає застосунок на тестовій базі, без фонових джобів.</summary>
public class TestApiFactory : WebApplicationFactory<global::Program>
{
    private readonly string _connectionString;

    public TestApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);

        // ConfigureTestServices, а не ConfigureServices: у minimal hosting колбеки
        // IWebHostBuilder виконуються ДО реєстрацій із Program, тому RemoveAll
        // прибирав би те, що AddHangfireServer і AddHostedService додають потім.
        builder.ConfigureTestServices(services =>
        {
            // Hangfire-сервер, RecurringJobScheduler і OutboxProcessor у тестах
            // лише заважають: вони чіпають ті самі рядки й дають фальшиві конфлікти.
            //
            // Плюс Hangfire кешує LoggerFactory у статичному GlobalJobFilters,
            // а WebApplicationFactory будує хост двічі — другий раз статика
            // тримає вже задиспоужений і падає ObjectDisposedException.
            services.RemoveAll<IHostedService>();
        });

        // Показує згенерований SQL разом зі значеннями параметрів —
        // без цього конфлікт xmin неможливо діагностувати
        builder.ConfigureLogging(logging => logging
            .AddConsole()
            .AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information));
    }

    /// <summary>Створює схему один раз перед тестами.</summary>
    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }
}
