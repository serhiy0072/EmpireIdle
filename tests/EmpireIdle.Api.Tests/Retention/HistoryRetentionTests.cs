using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Common.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Retention;

/// <summary>
/// Історія живе свій строк і зникає: звіти боїв і вирішені заявки старші за строк видаляються,
/// свіжі й ще відкриті лишаються — і так у всіх світах одразу.
/// </summary>
[Collection("postgres")]
public class HistoryRetentionTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public HistoryRetentionTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Purge_ShouldDeleteOnlyHistoryOlderThanItsRetention()
    {
        var now = DateTime.UtcNow;

        var oldReport = Report(now.AddDays(-31));
        var freshReport = Report(now.AddDays(-1));

        // Заявки в різних світах: прибирання не дивиться на поточний світ
        var oldRequest = new ClanRequest(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), ClanRequestKind.Application,
            now.AddDays(-9), now.AddDays(-12));
        var pendingRequest = new ClanRequest(Guid.NewGuid(), 2, Guid.NewGuid(), Guid.NewGuid(), ClanRequestKind.Application,
            now.AddDays(2), now.AddDays(-1));

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            context.BattleReports.AddRange(oldReport, freshReport);
            context.ClanRequests.AddRange(oldRequest, pendingRequest);
            await context.SaveChangesAsync();
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new PurgeStaleHistoryCommand());

        await using var check = _factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();

        var reports = await db.BattleReports.IgnoreQueryFilters().Where(r => r.Id == oldReport.Id || r.Id == freshReport.Id).Select(r => r.Id).ToListAsync();
        var requests = await db.ClanRequests.IgnoreQueryFilters().Where(r => r.Id == oldRequest.Id || r.Id == pendingRequest.Id).Select(r => r.Id).ToListAsync();

        Assert.Equal([freshReport.Id], reports);
        Assert.Equal([pendingRequest.Id], requests);
    }

    private static BattleReport Report(DateTime foughtAt)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1, "plain", "Нападник", 5, true, 100, 50, 1, foughtAt);
}
