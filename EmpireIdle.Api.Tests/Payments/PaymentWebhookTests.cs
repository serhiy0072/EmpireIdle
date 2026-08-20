using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Payments.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Payments;

/// <summary>
/// Вебхук Stripe приходить без токена. Глобальні query-фільтри читають
/// IServerContext, тож світ має відновлюватись із самого платежу —
/// інакше нарахування падає, гравець платить і не отримує gems.
/// </summary>
[Collection("postgres")]
public class PaymentWebhookTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public PaymentWebhookTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    /// <summary>Нарахування працює у scope без HTTP-контексту.</summary>
    [Fact]
    public async Task CompletePayment_ShouldCreditGems_WithoutHttpContext()
    {
        var (playerId, sessionId) = await SeedPendingPaymentAsync(gems: 100);

        await SendWithoutContextAsync(new CompletePaymentCommand(sessionId));

        Assert.Equal(100, await GemBalanceAsync(playerId));
    }

    /// <summary>
    /// Stripe ретраїть вебхук, доки не отримає 200 — повтор не має
    /// нараховувати gems удруге.
    /// </summary>
    [Fact]
    public async Task CompletePayment_ShouldIgnoreDuplicateWebhook()
    {
        var (playerId, sessionId) = await SeedPendingPaymentAsync(gems: 100);

        await SendWithoutContextAsync(new CompletePaymentCommand(sessionId));
        await SendWithoutContextAsync(new CompletePaymentCommand(sessionId));

        Assert.Equal(100, await GemBalanceAsync(playerId));
    }

    [Fact]
    public async Task CompletePayment_ShouldMarkPaymentCompleted()
    {
        var (playerId, sessionId) = await SeedPendingPaymentAsync(gems: 100);

        await SendWithoutContextAsync(new CompletePaymentCommand(sessionId));

        using var scope = _factory.Services.CreateScope();
        SetServer(scope);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var payment = await context.Payments.AsNoTracking().FirstAsync(p => p.SessionId == sessionId);

        Assert.NotNull(payment.CompletedAt);
    }

    /// <summary>
    /// Ключова перевірка: scope навмисно НЕ отримує світ ззовні.
    /// Якщо хендлер не викличе UseServer із платежу, тест впаде
    /// на UnauthorizedAccessException — саме так поводився продакшен-код.
    /// </summary>
    private async Task SendWithoutContextAsync(IRequest command)
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Send(command);
    }

    private async Task<(Guid PlayerId, string SessionId)> SeedPendingPaymentAsync(int gems)
    {
        using var scope = _factory.Services.CreateScope();
        SetServer(scope);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = Guid.NewGuid().ToString();
        var playerId = Guid.NewGuid();
        var sessionId = $"cs_test_{Guid.NewGuid():N}";

        context.Players.Add(new Player(
            id: playerId,
            username: $"player-{playerId:N}",
            email: $"{playerId:N}@test.local",
            userId: userId,
            serverId: ServerId));

        context.PlayerWallets.Add(new PlayerWallet(Guid.NewGuid(), userId));

        context.Payments.Add(new Payment(Guid.NewGuid(), playerId, ServerId, "pack_small",
            gems, 99, "usd", sessionId, DateTime.UtcNow));

        await context.SaveChangesAsync();

        return (playerId, sessionId);
    }

    private async Task<int> GemBalanceAsync(Guid playerId)
    {
        using var scope = _factory.Services.CreateScope();
        SetServer(scope);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await context.Players.AsNoTracking().FirstAsync(p => p.Id == playerId);
        var wallet = await context.PlayerWallets.AsNoTracking().FirstAsync(w => w.UserId == player.UserId);

        return wallet.GemBalance.Value;
    }

    private static void SetServer(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
}
