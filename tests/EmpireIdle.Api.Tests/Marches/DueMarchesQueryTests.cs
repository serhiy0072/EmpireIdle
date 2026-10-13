using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Marches;

/// <summary>
/// Сканер бере марші, чий час настав. Табір (§2.5) стоїть до відкликання, а його
/// «час прибуття» — момент, коли він став; без фільтра сканер обробляв би його щопрогону.
/// </summary>
[Collection("postgres")]
public class DueMarchesQueryTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private static readonly Dictionary<UnitStackKey, int> Army = new() { [new UnitStackKey("infantry", 1)] = 10 };

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public DueMarchesQueryTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task GetDueAsync_ShouldSkipCamps()
    {
        var now = DateTime.UtcNow;
        var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

        var due = Attack(garrison.Id, now);
        var camp = Attack(garrison.Id, now);
        camp.Camp(now.AddMinutes(-1));

        await using (var seed = CreateScope(out _, out var context))
        {
            context.Garrisons.Add(garrison);
            context.Marches.AddRange(due, camp);
            await context.SaveChangesAsync();
        }

        await using var scope = CreateScope(out var marches, out _);
        var ids = (await marches.GetDueAsync(now, batchSize: 10_000)).Select(m => m.Id).ToList();

        Assert.Contains(due.Id, ids);
        Assert.DoesNotContain(camp.Id, ids);
    }

    /// <summary>Атака, що вже мала прибути: час настав хвилину тому.</summary>
    private static March Attack(Guid garrisonId, DateTime now)
        => new(Guid.NewGuid(), ServerId, garrisonId, 1, 1, 5, 5,
            MarchTargetType.Village, Guid.NewGuid(), Army, now.AddMinutes(-1), now.AddMinutes(-20), MarchIntent.Attack);

    private AsyncServiceScope CreateScope(out IMarchRepository marches, out AppDbContext context)
    {
        var scope = _factory.Services.CreateAsyncScope();

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        marches = scope.ServiceProvider.GetRequiredService<IMarchRepository>();
        context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return scope;
    }
}
