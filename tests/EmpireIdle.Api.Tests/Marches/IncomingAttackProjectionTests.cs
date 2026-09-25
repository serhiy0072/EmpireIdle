using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Marches;

/// <summary>
/// Проєкція ворожих маршів збирає нападника й ціль із п'яти таблиць одним запитом.
/// На InMemory вона не перевіряється: важливо, що Postgres її перекладе й поверне те саме.
/// </summary>
[Collection("postgres")]
public class IncomingAttackProjectionTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private static readonly Dictionary<UnitStackKey, int> Army = new() { [new UnitStackKey("infantry", 1)] = 10 };

    private readonly PostgresFixture _postgres;
    private readonly Random _random = new();
    private TestApiFactory _factory = null!;

    public IncomingAttackProjectionTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Attack_OnAClanmateVillage_ShouldCarryAttackerTargetAndClans()
    {
        var now = DateTime.UtcNow;
        var (defenderClan, defenderTag) = await SeedClanAsync();
        var (attackerClan, attackerTag) = await SeedClanAsync();
        var defender = await SeedPlayerAsync(defenderClan);
        var attacker = await SeedPlayerAsync(attackerClan);

        var march = await SendAsync(attacker, MarchTargetType.Village, defender.Village.Id,
            defender.Village.X, defender.Village.Y, now);

        await using var context = CreateScope(out var marches);
        var attacks = await marches.GetIncomingAttacksAsync([defender.Player.Id], defenderClan);

        var attack = Assert.Single(attacks);
        Assert.Equal(march.Id, attack.MarchId);
        Assert.Equal(MarchTargetType.Village, attack.TargetType);
        Assert.Equal(defender.Village.Name, attack.TargetName);
        Assert.Equal(defender.Player.Id, attack.TargetOwnerId);
        Assert.Equal(defenderClan, attack.DefenderClanId);
        Assert.Equal((attacker.Village.X, attacker.Village.Y), (attack.FromX, attack.FromY));
        Assert.Equal(attacker.Player.Id, attack.AttackerPlayerId);
        Assert.Equal(attacker.Player.Username, attack.AttackerName);
        Assert.Equal(attackerTag, attack.AttackerClanTag);
        Assert.NotEqual(defenderTag, attack.AttackerClanTag);
    }

    [Fact]
    public async Task Attack_OnAStructure_ShouldBeNamedByTheOwningClanTag()
    {
        var now = DateTime.UtcNow;
        var (defenderClan, defenderTag) = await SeedClanAsync();
        var attacker = await SeedPlayerAsync(clanId: null);
        var structure = await SeedStructureAsync(defenderClan, now);

        await SendAsync(attacker, MarchTargetType.ClanStructure, structure.Id, structure.X, structure.Y, now);

        await using var context = CreateScope(out var marches);
        var attack = Assert.Single(await marches.GetIncomingAttacksAsync([], defenderClan));

        Assert.Equal(defenderTag, attack.TargetName);
        Assert.Null(attack.TargetOwnerId);
        Assert.Equal(defenderClan, attack.DefenderClanId);
        Assert.Null(attack.AttackerClanTag);
    }

    /// <summary>Підкріплення й марші на чужих не тривожать: у списку лише напади на своїх.</summary>
    [Fact]
    public async Task Only_AttacksOnTheDefenders_ShouldBeListed()
    {
        var now = DateTime.UtcNow;
        var defender = await SeedPlayerAsync(clanId: null);
        var stranger = await SeedPlayerAsync(clanId: null);
        var attacker = await SeedPlayerAsync(clanId: null);

        var attack = await SendAsync(attacker, MarchTargetType.Village, defender.Village.Id, 1, 1, now);
        await SendAsync(attacker, MarchTargetType.Village, defender.Village.Id, 1, 1, now, MarchIntent.Reinforce);
        await SendAsync(attacker, MarchTargetType.Village, stranger.Village.Id, 1, 1, now);

        await using var context = CreateScope(out var marches);

        var attacks = await marches.GetIncomingAttacksAsync([defender.Player.Id], clanId: null);

        Assert.Equal([attack.Id], attacks.Select(a => a.MarchId));
        Assert.Null(await marches.GetIncomingAttackAsync(Guid.NewGuid()));
        Assert.NotNull(await marches.GetIncomingAttackAsync(attack.Id));
    }

    private AsyncServiceScope CreateScope(out IMarchRepository marches)
    {
        var scope = _factory.Services.CreateAsyncScope();

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        marches = scope.ServiceProvider.GetRequiredService<IMarchRepository>();

        return scope;
    }

    private AppDbContext CreateContext()
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);

        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    // Спільна база на всю колекцію: координати й назви в кожного тесту свої
    private int Coordinate() => _random.Next(1, 9_000);

    private async Task<(Guid Id, string Tag)> SeedClanAsync()
    {
        await using var context = CreateContext();

        var tag = Guid.NewGuid().ToString("N")[..4];
        var clan = new Clan(Guid.NewGuid(), ServerId, $"Clan {tag}", tag, Guid.NewGuid(), DateTime.UtcNow);

        context.Clans.Add(clan);
        await context.SaveChangesAsync();

        return (clan.Id, tag);
    }

    private async Task<(Player Player, Village Village, Garrison Garrison)> SeedPlayerAsync(Guid? clanId)
    {
        await using var context = CreateContext();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var player = new Player(Guid.NewGuid(), $"p{suffix}", $"{suffix}@test.local", $"user-{suffix}", DateTime.UtcNow, ServerId);
        if (clanId is { } id)
            player.JoinClan(id);

        var village = new Village(Guid.NewGuid(), player.Id, $"Village {suffix}", [], Coordinate(), Coordinate(), ServerId);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, ServerId);

        context.Players.Add(player);
        context.Villages.Add(village);
        context.Garrisons.Add(garrison);
        await context.SaveChangesAsync();

        return (player, village, garrison);
    }

    private async Task<ClanStructure> SeedStructureAsync(Guid clanId, DateTime now)
    {
        await using var context = CreateContext();

        var structureId = Guid.NewGuid();
        var garrison = Garrison.ForStructure(Guid.NewGuid(), structureId, ServerId);
        var structure = new ClanStructure(structureId, ServerId, clanId, Coordinate(), Coordinate(), garrison.Id,
            Guid.NewGuid(), TimeSpan.FromHours(1), now);

        context.Garrisons.Add(garrison);
        context.ClanStructures.Add(structure);
        await context.SaveChangesAsync();

        return structure;
    }

    private async Task<March> SendAsync((Player Player, Village Village, Garrison Garrison) from,
        MarchTargetType targetType, Guid targetId, int targetX, int targetY, DateTime now,
        MarchIntent intent = MarchIntent.Attack)
    {
        await using var context = CreateContext();

        var march = new March(Guid.NewGuid(), ServerId, from.Garrison.Id, heroId: null,
            from.Village.X, from.Village.Y, targetX, targetY, targetType, targetId, Army,
            now.AddMinutes(30), now, intent);

        context.Marches.Add(march);
        await context.SaveChangesAsync();

        return march;
    }
}
