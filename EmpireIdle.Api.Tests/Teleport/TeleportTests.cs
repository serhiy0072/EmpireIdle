using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.API.DTOs;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;

namespace EmpireIdle.Api.Tests.Teleport;

/// <summary>
/// Контейнер, застосунок і один зареєстрований гравець — на весь клас,
/// як в IdempotencyFixture: піднімати хост на кожен тест дорого,
/// а реєстрації впираються в лімітер "auth".
/// </summary>
public class TeleportFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public Guid PlayerId { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new TestApiFactory(_container.GetConnectionString());
        await Factory.MigrateAsync();

        Client = Factory.CreateClient();

        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            UserName: $"tp{Guid.NewGuid():N}"[..12],
            Email: $"tp-{Guid.NewGuid():N}@test.local",
            Password: "Password123"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        PlayerId = auth!.PlayerId;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Кладе телепорти в інвентар напряму — шляху видачі в тестах немає.
    /// Оновлює наявний стек, бо фікстура одна на клас, а індекс
    /// (PlayerId, ItemKey) унікальний.
    /// </summary>
    public async Task GrantTeleportsAsync(int count)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await context.PlayerItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.PlayerId == PlayerId && i.ItemKey == "teleport");

        if (existing is null)
            context.PlayerItems.Add(new PlayerItem(Guid.NewGuid(), PlayerId, "teleport", count));
        else
            existing.Add(count);

        await context.SaveChangesAsync();
    }

    /// <summary>Координати села гравця просто зараз.</summary>
    public async Task<(int X, int Y)> GetVillageCellAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var village = await context.Villages.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstAsync(v => v.PlayerId == PlayerId);

        return (village.X, village.Y);
    }

    /// <summary>
    /// Вільна придатна клітина в межах туману. Шукаємо перебором від центру:
    /// випадковий кидок промазував би по непрохідних, і тест ставав би плаваючим.
    /// </summary>
    public async Task<(int X, int Y)> FindFreeCellAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var geometry = provider.GetRequiredService<WorldGeometry>();
        var terrain = provider.GetRequiredService<TerrainGenerator>();
        var context = provider.GetRequiredService<AppDbContext>();

        var (cx, cy) = geometry.Centre;
        var boundary = geometry.SettlementBoundary(1);

        var occupied = await context.MapCells.AsNoTracking()
            .IgnoreQueryFilters()
            .Select(c => new { c.X, c.Y })
            .ToListAsync();

        for (var d = 1; d <= boundary; d++)
        {
            for (var dx = -d; dx <= d; dx++)
            {
                var x = cx + dx;
                var y = cy + d;

                if (!terrain.IsHabitable(1, x, y))
                    continue;

                if (occupied.Any(c => c.X == x && c.Y == y))
                    continue;

                return (x, y);
            }
        }

        throw new InvalidOperationException("No free habitable cell within the fog boundary.");
    }

    private static string NewKey() => $"tp-{Guid.NewGuid():N}";

    /// <summary>Використати предмет із валідним ключем ідемпотентності.</summary>
    public Task<HttpResponseMessage> UseAsync(UseItemRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"/api/inventory/{PlayerId}/use")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Add("Idempotency-Key", NewKey());

        return Client.SendAsync(message);
    }
}

public class TeleportTests : IClassFixture<TeleportFixture>
{
    private readonly TeleportFixture _fixture;

    public TeleportTests(TeleportFixture fixture) => _fixture = fixture;

    /// <summary>Телепорт переносить село й звільняє стару клітину.</summary>
    [Fact]
    public async Task Teleport_ShouldMoveTheVillageAndFreeTheOldCell()
    {
        await _fixture.GrantTeleportsAsync(1);

        var before = await _fixture.GetVillageCellAsync();
        var target = await _fixture.FindFreeCellAsync();

        var response = await _fixture.UseAsync(
            new UseItemRequest("teleport", 1, TargetX: target.X, TargetY: target.Y));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await _fixture.GetVillageCellAsync();
        after.Should().Be(target);
        after.Should().NotBe(before);
    }

    /// <summary>Переїзд на власну клітину — не конфлікт, а безглузда дія.</summary>
    [Fact]
    public async Task Teleport_ShouldReject_WhenTargetIsTheCurrentCell()
    {
        await _fixture.GrantTeleportsAsync(1);

        var current = await _fixture.GetVillageCellAsync();

        var response = await _fixture.UseAsync(
            new UseItemRequest("teleport", 1, TargetX: current.X, TargetY: current.Y));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Клітина за межею туману недоступна, навіть якщо вона в межах карти.</summary>
    [Fact]
    public async Task Teleport_ShouldReject_WhenTheCellIsBeyondTheFog()
    {
        await _fixture.GrantTeleportsAsync(1);

        using var scope = _fixture.Factory.Services.CreateScope();
        var geometry = scope.ServiceProvider.GetRequiredService<WorldGeometry>();

        var (cx, cy) = geometry.Centre;
        var beyond = geometry.SettlementBoundary(1) + 5;

        var response = await _fixture.UseAsync(
            new UseItemRequest("teleport", 1, TargetX: cx + beyond, TargetY: cy));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Одна координата без другої — помилка клієнта, а не падіння ефекту.</summary>
    [Fact]
    public async Task Teleport_ShouldReject_WhenOnlyOneCoordinateIsGiven()
    {
        await _fixture.GrantTeleportsAsync(1);

        var response = await _fixture.UseAsync(
            new UseItemRequest("teleport", 1, TargetX: 10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
