using EmpireIdle.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Антиспам чату тримається на блокуванні відправника: другий запит того самого гравця
/// чекає, поки перший закомітить своє повідомлення, і вже тоді рахує ліміт.
/// </summary>
[Collection("postgres")]
public class ChatSenderLockTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public ChatSenderLockTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AsyncServiceScope CreateScope(out IChatRepository chat, out IUnitOfWork unitOfWork)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        chat = scope.ServiceProvider.GetRequiredService<IChatRepository>();
        unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        return scope;
    }

    [Fact]
    public async Task LockSender_ShouldHoldASecondSender_UntilTheFirstCommits()
    {
        var senderId = Guid.NewGuid();

        await using var first = CreateScope(out var firstChat, out var firstUnitOfWork);
        await firstUnitOfWork.BeginTransactionAsync();
        await firstChat.LockSenderAsync(senderId);

        await using var second = CreateScope(out var secondChat, out var secondUnitOfWork);
        await secondUnitOfWork.BeginTransactionAsync();
        var waiting = secondChat.LockSenderAsync(senderId);

        // Поки перший не закомітив, другий стоїть на блокуванні
        await Task.Delay(300);
        Assert.False(waiting.IsCompleted);

        await firstUnitOfWork.CommitTransactionAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        await secondUnitOfWork.CommitTransactionAsync();
    }

    [Fact]
    public async Task LockSender_ShouldNotBlock_ADifferentSender()
    {
        await using var first = CreateScope(out var firstChat, out var firstUnitOfWork);
        await firstUnitOfWork.BeginTransactionAsync();
        await firstChat.LockSenderAsync(Guid.NewGuid());

        await using var second = CreateScope(out var secondChat, out var secondUnitOfWork);
        await secondUnitOfWork.BeginTransactionAsync();
        await secondChat.LockSenderAsync(Guid.NewGuid()).WaitAsync(TimeSpan.FromSeconds(5));

        await secondUnitOfWork.CommitTransactionAsync();
        await firstUnitOfWork.CommitTransactionAsync();
    }
}
