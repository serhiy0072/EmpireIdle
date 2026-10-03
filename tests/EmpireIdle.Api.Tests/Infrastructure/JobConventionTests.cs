using EmpireIdle.API.Jobs;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Повторювані джоби на деплої мають зупинятися на межі елемента: кожен приймає токен
/// зупинки від Hangfire, а раннер не ковтає скасування як звичайну помилку.
/// </summary>
public class JobConventionTests
{
    [Fact]
    public void EveryJob_ShouldTakeTheShutdownToken()
    {
        var jobs = typeof(ServerJobRunner).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ServerJobRunner).Namespace && t.Name.EndsWith("Job"))
            .ToList();

        Assert.NotEmpty(jobs);

        var withoutToken = jobs
            .Where(t => t.GetMethod("RunAsync") is not { } run
                        || run.GetParameters().All(p => p.ParameterType != typeof(CancellationToken)))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(withoutToken);
    }

    private static ServerJobRunner Runner()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped<IServerContext, FixedServerContext>()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<JobConventionTests>())
            .BuildServiceProvider();

        var catalog = new GameCatalog(new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
            ActiveServerIds = [1, 2]
        });

        return new ServerJobRunner(services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ServerJobRunner>.Instance, catalog);
    }

    /// <summary>Зупинка — не помилка світу: прогін виходить, а не йде далі й не логує Error.</summary>
    [Fact]
    public async Task Runner_ShouldStop_WhenShutdownIsRequested()
    {
        using var shutdown = new CancellationTokenSource();
        var processed = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner().ForEachItemAsync(
            "test",
            (_, _) => Task.FromResult<IReadOnlyList<int>>([1, 2, 3]),
            (_, _, _) =>
            {
                processed++;
                shutdown.Cancel();
                return Task.CompletedTask;
            },
            shutdown.Token));

        Assert.Equal(1, processed);
    }

    /// <summary>Помилка одного елемента без зупинки — звичайна: решта проходить.</summary>
    [Fact]
    public async Task Runner_ShouldContinue_AfterAnItemFails()
    {
        var processed = 0;

        await Runner().ForEachItemAsync(
            "test",
            (_, _) => Task.FromResult<IReadOnlyList<int>>([1, 2, 3]),
            (_, item, _) =>
            {
                processed++;
                return item == 2 ? throw new InvalidOperationException("boom") : Task.CompletedTask;
            });

        // Два світи × три елементи
        Assert.Equal(6, processed);
    }

    /// <summary>Токен зупинки доходить до дії: інакше Send у джобі не дізнався б про деплой.</summary>
    [Fact]
    public async Task Runner_ShouldHandTheShutdownTokenToTheAction()
    {
        using var shutdown = new CancellationTokenSource();
        var received = new List<CancellationToken>();

        await Runner().ForEachServerAsync("test", (_, _, ct) =>
        {
            received.Add(ct);
            return Task.CompletedTask;
        }, shutdown.Token);

        Assert.All(received, ct => Assert.Equal(shutdown.Token, ct));
        Assert.Equal(2, received.Count);
    }

    /// <summary>Дані без світу обробляються раз за тік, а не на кожен із двох світів.</summary>
    [Fact]
    public async Task RunOnce_ShouldRunTheActionOnceWithTheShutdownToken()
    {
        using var shutdown = new CancellationTokenSource();
        var received = new List<CancellationToken>();

        await Runner().RunOnceAsync("test", (_, ct) =>
        {
            received.Add(ct);
            return Task.CompletedTask;
        }, shutdown.Token);

        Assert.Equal(shutdown.Token, Assert.Single(received));
    }

    /// <summary>Помилка без зупинки логується: джоб не падає, наступний тік повторить.</summary>
    [Fact]
    public async Task RunOnce_ShouldSwallowAFailure()
        => Assert.Null(await Record.ExceptionAsync(() => Runner().RunOnceAsync("test",
            (_, _) => throw new InvalidOperationException("boom"))));

    [Fact]
    public async Task RunOnce_ShouldStop_WhenShutdownIsRequested()
    {
        using var shutdown = new CancellationTokenSource();
        shutdown.Cancel();
        var ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Runner().RunOnceAsync("test", (_, _) =>
        {
            ran = true;
            return Task.CompletedTask;
        }, shutdown.Token));

        Assert.False(ran);
    }

    /// <summary>Світ без HTTP-запиту: раннер лише ставить його в scope.</summary>
    private sealed class FixedServerContext : IServerContext
    {
        public int ServerId { get; private set; }

        public void UseServer(int serverId) => ServerId = serverId;
    }
}
