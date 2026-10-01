using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Фоновий джоб зберігає світ пачками: конфлікт xmin з гравцем має відкотити лише свою пачку.
/// TrySaveChangesAsync повертає false і скидає відстежене, а не кидає виняток на весь прогін.
/// </summary>
[Collection("postgres")]
public class TrySaveChangesTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public TrySaveChangesTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AsyncServiceScope CreateScope(out AppDbContext context, out IUnitOfWork unitOfWork)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return scope;
    }

    [Fact]
    public async Task TrySaveChanges_ShouldReturnFalseAndDetach_OnAConcurrencyConflict()
    {
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Before", [], 1, 1, ServerId);

        await using (var seed = CreateScope(out var context, out _))
        {
            context.Villages.Add(village);
            await context.SaveChangesAsync();
        }

        await using var job = CreateScope(out var jobContext, out var jobUnitOfWork);
        var stale = await jobContext.Villages.SingleAsync(v => v.Id == village.Id);

        // Гравець встиг змінити село раніше за джоб
        await using (var player = CreateScope(out var playerContext, out _))
        {
            var fresh = await playerContext.Villages.SingleAsync(v => v.Id == village.Id);
            fresh.RelocateTo(20, 20, DateTime.UtcNow);
            await playerContext.SaveChangesAsync();
        }

        stale.RelocateTo(30, 30, DateTime.UtcNow);

        Assert.False(await jobUnitOfWork.TrySaveChangesAsync());
        Assert.Empty(jobContext.ChangeTracker.Entries());

        await using var check = CreateScope(out var reader, out _);
        Assert.Equal(20, (await reader.Villages.SingleAsync(v => v.Id == village.Id)).X);
    }

    [Fact]
    public async Task TrySaveChanges_ShouldReturnTrue_WithoutAConflict()
    {
        await using var scope = CreateScope(out var context, out var unitOfWork);

        context.Villages.Add(new Village(Guid.NewGuid(), Guid.NewGuid(), "Fresh", [], 1, 1, ServerId));

        Assert.True(await unitOfWork.TrySaveChangesAsync());
    }
}
