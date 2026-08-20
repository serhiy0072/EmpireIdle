using EmpireIdle.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>Піднімає застосунок на тестовій базі, без фонових джобів.</summary>
public class TestApiFactory : WebApplicationFactory<global::Program>
{
    private readonly string _connectionString;

    public TestApiFactory(string connectionString) => _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);

        builder.ConfigureServices(services =>
        {
            // OutboxProcessor і Hangfire у тестах паралелізму лише заважають:
            // вони чіпають ті самі рядки й дають фальшиві конфлікти
            services.RemoveAll<IHostedService>();
        });
    }

    /// <summary>Створює схему один раз перед тестами.</summary>
    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }
}
