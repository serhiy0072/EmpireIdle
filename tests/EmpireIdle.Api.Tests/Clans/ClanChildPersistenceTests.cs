using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Clans;

/// <summary>
/// Дочірні рядки клану, додані до вже збереженого агрегату, мають
/// потрапляти в базу. Кожен тест — окремий шлях, що падав із 409.
/// </summary>
[Collection("postgres")]
public class ClanChildPersistenceTests : IAsyncLifetime
{
    private const int ServerId = 1;
    private const int Capacity = 200;

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public ClanChildPersistenceTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Join_ShouldInsertMember_WhenClanAlreadySaved()
    {
        var founderId = Guid.NewGuid();
        var clanId = await SeedClanAsync(founderId);
        var newcomerId = Guid.NewGuid();

        await using (var context = CreateContext())
        {
            var clan = await LoadClanAsync(context, clanId);
            clan.Join(newcomerId, Capacity, DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var saved = await LoadClanAsync(verify, clanId);

        Assert.Contains(saved.Members, m => m.PlayerId == newcomerId);
    }

    [Fact]
    public async Task CreateRole_ShouldInsertRole_WhenClanAlreadySaved()
    {
        var founderId = Guid.NewGuid();
        var clanId = await SeedClanAsync(founderId);
        Guid roleId;

        await using (var context = CreateContext())
        {
            var clan = await LoadClanAsync(context, clanId);
            roleId = clan.CreateRole(founderId, "Scouts", 20, ClanPermission.Recruit, DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var saved = await LoadClanAsync(verify, clanId);

        Assert.Contains(saved.Roles, r => r.Id == roleId);
    }

    [Fact]
    public async Task AcceptHelp_ShouldInsertContribution_WhenRequestAlreadySaved()
    {
        var now = DateTime.UtcNow;
        var request = new ClanHelpRequest(Guid.NewGuid(), ServerId, Guid.NewGuid(), Guid.NewGuid(),
            ClanHelpTarget.Construction, Guid.NewGuid(), TimeSpan.FromHours(1), now.AddHours(1), now);
        var helperId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            seed.ClanHelpRequests.Add(request);
            await seed.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var loaded = await context.ClanHelpRequests.Include(r => r.Helpers).FirstAsync(r => r.Id == request.Id);
            loaded.AcceptHelp(helperId, 0.1, 10, now);
            await context.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var saved = await verify.ClanHelpRequests.Include(r => r.Helpers).FirstAsync(r => r.Id == request.Id);

        Assert.Contains(saved.Helpers, h => h.HelperId == helperId);
    }

    private AppDbContext CreateContext()
    {
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);

        return context;
    }

    private static Task<Clan> LoadClanAsync(AppDbContext context, Guid id)
        => context.Clans.Include(c => c.Members).Include(c => c.Roles).FirstAsync(c => c.Id == id);

    private async Task<Guid> SeedClanAsync(Guid founderId)
    {
        await using var context = CreateContext();

        // Назва й тег унікальні в межах світу — кожен тест бере свої
        var suffix = Guid.NewGuid().ToString("N")[..4];
        var clan = new Clan(Guid.NewGuid(), ServerId, $"Test {suffix}", suffix, founderId, DateTime.UtcNow);

        context.Clans.Add(clan);
        await context.SaveChangesAsync();

        return clan.Id;
    }
}
