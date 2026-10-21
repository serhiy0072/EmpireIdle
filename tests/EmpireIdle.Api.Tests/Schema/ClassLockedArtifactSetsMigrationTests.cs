using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EmpireIdle.Api.Tests.Schema;

/// <summary>
/// Data-міграція наборів по класах (рішення 15.10.2026) на справжніх рядках: бонуси заточки переходять
/// на нові ступені (16.10.2026), а набір чужого класу знімається з героя в рюкзак.
/// Окрема база в тому самому контейнері — решта колекції не пережила б відкату міграцій.
/// </summary>
[Collection("postgres")]
public class ClassLockedArtifactSetsMigrationTests
{
    private const string Before = "20261015231421_DropGarrisonLeader";
    private const string Migration = "20261015232942_ClassLockedArtifactSets";

    private readonly PostgresFixture _postgres;

    public ClassLockedArtifactSetsMigrationTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record ItemRow(Guid Id, Guid? EquippedByHeroId, int SlotIndex);

    private sealed record StatRow(Guid Id, string StatKey, double Value);

    [Fact]
    public async Task Up_ShouldConvertDeadStats_AndUnequipOnlyForeignClassSets()
    {
        await using var factory = new TestApiFactory(await CreateDatabaseAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(Before);

        // Ліра — лучниця (mage_iselle теж лучниця, хоч ключ і «маг»): dawn тепер лицарський, ember — лучницький
        var archer = await InsertHeroAsync(context, "archer_lyra");
        var iselle = await InsertHeroAsync(context, "mage_iselle");
        var foreign = await InsertItemAsync(context, "dawn_necklace_common", archer, slotIndex: 0);
        var own = await InsertItemAsync(context, "ember_ring_rare", archer, slotIndex: 2);
        var ownByOddKey = await InsertItemAsync(context, "gale_belt_unique", iselle, slotIndex: 3);
        var loose = await InsertItemAsync(context, "frost_crown_common", equippedBy: null, slotIndex: 0);

        var speed = await InsertStatAsync(context, loose, "AttackSpeed", 6);
        var cooldown = await InsertStatAsync(context, loose, "CooldownReduction", 4);
        var unitCrit = await InsertStatAsync(context, own, "UnitCritChance", 3);
        var health = await InsertStatAsync(context, own, "UnitHealthPercent", 9);
        var crit = await InsertStatAsync(context, foreign, "CritChance", 2);

        await migrator.MigrateAsync(Migration);

        var items = (await ReadItemsAsync(context)).ToDictionary(r => r.Id);
        Assert.Null(items[foreign].EquippedByHeroId);
        Assert.Equal(archer, items[own].EquippedByHeroId);
        Assert.Equal(iselle, items[ownByOddKey].EquippedByHeroId);

        var stats = (await ReadStatsAsync(context)).ToDictionary(r => r.Id);
        Assert.Equal(("AttackSpeed", 12.0), (stats[speed].StatKey, stats[speed].Value));
        Assert.Equal(("CooldownReduction", 4.0), (stats[cooldown].StatKey, stats[cooldown].Value));
        Assert.Equal(("CritChance", 14.25), (stats[unitCrit].StatKey, stats[unitCrit].Value));
        Assert.Equal(("TroopsDefense", 6.0), (stats[health].StatKey, stats[health].Value));
        Assert.Equal(("CritChance", 9.5), (stats[crit].StatKey, stats[crit].Value));
    }

    /// <summary>
    /// Відкат повертає старі ключі й значення статів, щоб попередня версія коду їх знайшла; колишній крит
    /// юнітів лишається критом героя — після злиття їх не розрізнити.
    /// </summary>
    [Fact]
    public async Task Down_ShouldRestoreTheOldStats()
    {
        await using var factory = new TestApiFactory(await CreateDatabaseAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(Before);

        var item = await InsertItemAsync(context, "frost_crown_common", equippedBy: null, slotIndex: 0);
        var health = await InsertStatAsync(context, item, "UnitHealthPercent", 9);
        var crit = await InsertStatAsync(context, item, "CritChance", 2);
        var speed = await InsertStatAsync(context, item, "AttackSpeed", 6);

        await migrator.MigrateAsync(Migration);
        await migrator.MigrateAsync(Before);

        var stats = (await ReadStatsAsync(context)).ToDictionary(r => r.Id);
        Assert.Equal(("UnitHealthPercent", 9.0), (stats[health].StatKey, stats[health].Value));
        Assert.Equal(("CritChance", 2.0), (stats[crit].StatKey, stats[crit].Value));
        Assert.Equal(("AttackSpeed", 6.0), (stats[speed].StatKey, stats[speed].Value));
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"class_locked_sets_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(_postgres.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = name }.ConnectionString;
    }

    private static async Task<Guid> InsertHeroAsync(AppDbContext context, string heroKey)
    {
        var id = Guid.NewGuid();

        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "Heroes"
                ("Id", "PlayerId", "ServerId", "HeroKey", "Tier", "NativeTier", "Level", "StarParts", "State",
                 "WeaponLevel", "WeaponShards", "StationedGarrisonId", "AcquiredAt", "UpdatedAt")
            VALUES
                ({id}, {Guid.NewGuid()}, 1, {heroKey}, 1, 1, 1, 0, 0,
                 0, 0, {Guid.NewGuid()}, now(), now())
            """);

        return id;
    }

    private static async Task<Guid> InsertItemAsync(AppDbContext context, string itemKey, Guid? equippedBy, int slotIndex)
    {
        var id = Guid.NewGuid();

        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "EquipmentItems"
                ("Id", "PlayerId", "ServerId", "ItemKey", "Slot", "SlotIndex", "Rarity", "Level", "Experience",
                 "Mastery", "IsBroken", "IsOnMarket", "EquippedByHeroId", "AcquiredAt", "UpdatedAt")
            VALUES
                ({id}, {Guid.NewGuid()}, 1, {itemKey}, 2, {slotIndex}, 1, 0, 0,
                 0, false, false, {equippedBy}, now(), now())
            """);

        return id;
    }

    private static async Task<Guid> InsertStatAsync(AppDbContext context, Guid itemId, string statKey, double value)
    {
        var id = Guid.NewGuid();

        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "EquipmentStats" ("Id", "EquipmentItemId", "StatKey", "Position", "Value")
            VALUES ({id}, {itemId}, {statKey}, 2, {value})
            """);

        return id;
    }

    private static async Task<List<ItemRow>> ReadItemsAsync(AppDbContext context)
        => await context.Database
            .SqlQuery<ItemRow>($"""SELECT "Id", "EquippedByHeroId", "SlotIndex" FROM "EquipmentItems" """)
            .ToListAsync();

    private static async Task<List<StatRow>> ReadStatsAsync(AppDbContext context)
        => await context.Database
            .SqlQuery<StatRow>($"""SELECT "Id", "StatKey", "Value" FROM "EquipmentStats" """)
            .ToListAsync();
}
