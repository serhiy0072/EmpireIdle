using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Idempotency;

/// <summary>
/// Кожна успішна команда лишає запис із відповіддю. Без чистки таблиця росла б вічно;
/// чистка ж не має чіпати свіжих записів — повтор із тим самим ключем має отримати ту саму відповідь.
/// </summary>
[Collection("postgres")]
public class IdempotencyRetentionTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public IdempotencyRetentionTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task PurgeCompleted_ShouldRemoveOnlyOldCompletedRecords()
    {
        var now = DateTime.UtcNow;
        var playerId = Guid.NewGuid();

        var oldCompleted = new IdempotencyRecord(Guid.NewGuid(), $"old-{Guid.NewGuid():N}", playerId, "Test", "{}", now.AddDays(-8));
        var freshCompleted = new IdempotencyRecord(Guid.NewGuid(), $"new-{Guid.NewGuid():N}", playerId, "Test", "{}", now.AddDays(-1));
        var oldReservation = new IdempotencyRecord(Guid.NewGuid(), $"res-{Guid.NewGuid():N}", playerId, "Test", null, now.AddDays(-8));

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            context.IdempotencyRecords.AddRange(oldCompleted, freshCompleted, oldReservation);
            await context.SaveChangesAsync();
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIdempotencyRepository>();

        await repository.PurgeCompletedAsync(now.AddDays(-7));

        var left = await scope.ServiceProvider.GetRequiredService<AppDbContext>().IdempotencyRecords
            .Where(r => r.PlayerId == playerId)
            .Select(r => r.Id)
            .ToListAsync();

        Assert.DoesNotContain(oldCompleted.Id, left);
        Assert.Contains(freshCompleted.Id, left);

        // Незавершені резерви — справа окремої чистки з власним строком
        Assert.Contains(oldReservation.Id, left);
    }
}
