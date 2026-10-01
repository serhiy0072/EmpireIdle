using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Список приватних розмов групується в БД: жвава розмова з одним співрозмовником
/// не витісняє давню з іншим, скільки б повідомлень у ній не було.
/// </summary>
[Collection("postgres")]
public class ChatConversationsQueryTests : IAsyncLifetime
{
    private const int ServerId = 1;
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public ChatConversationsQueryTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static ChatMessage Private(Guid from, Guid to, string text, DateTime sentAt)
        => new(Guid.NewGuid(), ServerId, ChatChannel.Private, null, from, to, text, "uk", sentAt);

    [Fact]
    public async Task GetLatestPrivateMessages_ShouldKeepAnOldConversation_BehindABusyOne()
    {
        var player = Guid.NewGuid();
        var busy = Guid.NewGuid();
        var quiet = Guid.NewGuid();

        await using (var seed = CreateScope(out var chat, out var unitOfWork))
        {
            await chat.AddAsync(Private(quiet, player, "давно", Now.AddDays(-2)));

            for (var i = 0; i < 60; i++)
                await chat.AddAsync(Private(i % 2 == 0 ? player : busy, i % 2 == 0 ? busy : player, $"#{i}", Now.AddMinutes(-60 + i)));

            await unitOfWork.SaveChangesAsync();
        }

        await using var read = CreateScope(out var reader, out _);

        var latest = await reader.GetLatestPrivateMessagesAsync(player, take: 2);

        Assert.Equal(2, latest.Count);
        Assert.Equal("#59", latest[0].Text);
        Assert.Equal("давно", latest[1].Text);
    }

    [Fact]
    public async Task GetLatestPrivateMessages_ShouldReturnTheNewestConversationsFirst_UpToTake()
    {
        var player = Guid.NewGuid();
        var partners = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        await using (var seed = CreateScope(out var chat, out var unitOfWork))
        {
            for (var i = 0; i < partners.Count; i++)
                await chat.AddAsync(Private(player, partners[i], $"to {i}", Now.AddMinutes(i)));

            await unitOfWork.SaveChangesAsync();
        }

        await using var read = CreateScope(out var reader, out _);

        var latest = await reader.GetLatestPrivateMessagesAsync(player, take: 2);

        Assert.Equal(["to 2", "to 1"], latest.Select(m => m.Text));
    }
}
