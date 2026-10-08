using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Marches;

/// <summary>
/// Завершені марші не накопичуються вічно: старші за межу видаляються разом зі складом,
/// свіжі завершені й активні лишаються.
/// </summary>
[Collection("postgres")]
public class MarchRetentionTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private static readonly Dictionary<UnitStackKey, int> Army = new() { [new UnitStackKey("infantry", 1)] = 10 };

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public MarchRetentionTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task DeleteCompletedBefore_ShouldRemoveOnlyOldCompletedMarches_WithTheirUnits()
    {
        var now = DateTime.UtcNow;
        var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid(), ServerId);

        var old = Reinforce(garrison.Id, now.AddDays(-3));
        old.Delivered(now.AddDays(-2));

        var recent = Reinforce(garrison.Id, now.AddHours(-2));
        recent.Delivered(now.AddHours(-1));

        var active = Reinforce(garrison.Id, now.AddDays(-3));

        await using (var seed = CreateScope(out _, out var context))
        {
            context.Garrisons.Add(garrison);
            context.Marches.AddRange(old, recent, active);
            await context.SaveChangesAsync();
        }

        await using (var scope = CreateScope(out var marches, out _))
            Assert.True(await marches.DeleteCompletedBeforeAsync(now.AddDays(-1)) >= 1);

        await using var check = CreateScope(out _, out var db);
        var left = await db.Marches.Where(m => m.GarrisonId == garrison.Id).Select(m => m.Id).ToListAsync();

        Assert.DoesNotContain(old.Id, left);
        Assert.Contains(recent.Id, left);
        Assert.Contains(active.Id, left);
        Assert.False(await db.Set<MarchUnit>().AnyAsync(u => u.MarchId == old.Id));
    }

    private static March Reinforce(Guid garrisonId, DateTime departedAt)
        => new(Guid.NewGuid(), ServerId, garrisonId, 1, 1, 5, 5,
            MarchTargetType.Village, Guid.NewGuid(), Army, departedAt.AddMinutes(20), departedAt, MarchIntent.Reinforce);

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
