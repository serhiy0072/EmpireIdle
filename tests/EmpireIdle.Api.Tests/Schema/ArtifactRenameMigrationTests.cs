using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EmpireIdle.Api.Tests.Schema;

/// <summary>
/// Data-міграція слотів за типом на справжніх рядках. Прогін на порожній
/// базі нічого не доводить: SQL, що не зачепив жодного рядка, «проходить»
/// навіть із помилкою в шаблоні.
///
/// Окрема база в тому самому контейнері: решта колекції ділить свою
/// й не пережила б відкату міграцій.
/// </summary>
[Collection("postgres")]
public class ArtifactRenameMigrationTests
{
    private const string Before = "20260923093606_AddDungeons";
    private const string Rename = "20260924053117_RenameArtifactPiecesForTypedSlots";

    private readonly PostgresFixture _postgres;

    public ArtifactRenameMigrationTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Row(Guid Id, string ItemKey, Guid? EquippedByHeroId, int SlotIndex);

    [Fact]
    public async Task Up_ShouldRenamePiecesAndUnequipArtifacts_ButLeaveWeaponsWorn()
    {
        var connectionString = await CreateDatabaseAsync();

        await using var factory = new TestApiFactory(connectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(Before);

        var hero = Guid.NewGuid();
        var necklace = await InsertAsync(context, "dawn_amulet_common", slot: 2, hero, slotIndex: 3);
        var crown = await InsertAsync(context, "ember_sigil_rare", slot: 2, equippedBy: null, slotIndex: 0);
        var belt = await InsertAsync(context, "obsidian_chime_unique", slot: 2, hero, slotIndex: 1);
        var ring = await InsertAsync(context, "frost_ring_common", slot: 2, hero, slotIndex: 0);
        var sword = await InsertAsync(context, "sword_iron", slot: 1, hero, slotIndex: 0);

        await migrator.MigrateAsync(Rename);

        var rows = (await ReadAsync(context)).ToDictionary(r => r.Id);

        Assert.Equal("dawn_necklace_common", rows[necklace].ItemKey);
        Assert.Equal("ember_crown_rare", rows[crown].ItemKey);
        Assert.Equal("obsidian_belt_unique", rows[belt].ItemKey);
        Assert.Equal("frost_ring_common", rows[ring].ItemKey);

        // Усі артефакти зняті, зброя лишилась на героєві
        Assert.All(new[] { necklace, belt, ring }, id =>
        {
            Assert.Null(rows[id].EquippedByHeroId);
            Assert.Equal(0, rows[id].SlotIndex);
        });
        Assert.Equal(hero, rows[sword].EquippedByHeroId);
        Assert.Equal("sword_iron", rows[sword].ItemKey);
    }

    /// <summary>Відкат повертає ключі, щоб попередня версія коду знову знайшла предмети в конфігу.</summary>
    [Fact]
    public async Task Down_ShouldRestoreTheOldKeys()
    {
        var connectionString = await CreateDatabaseAsync();

        await using var factory = new TestApiFactory(connectionString);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(Before);
        var necklace = await InsertAsync(context, "dawn_amulet_common", slot: 2, equippedBy: null, slotIndex: 0);
        var belt = await InsertAsync(context, "gale_chime_rare", slot: 2, equippedBy: null, slotIndex: 0);

        await migrator.MigrateAsync(Rename);
        await migrator.MigrateAsync(Before);

        var rows = (await ReadAsync(context)).ToDictionary(r => r.Id);

        Assert.Equal("dawn_amulet_common", rows[necklace].ItemKey);
        Assert.Equal("gale_chime_rare", rows[belt].ItemKey);
    }

    private async Task<string> CreateDatabaseAsync()
    {
        var name = $"artifact_rename_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(_postgres.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = name }.ConnectionString;
    }

    private static async Task<Guid> InsertAsync(AppDbContext context, string itemKey, int slot, Guid? equippedBy, int slotIndex)
    {
        var id = Guid.NewGuid();

        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO "EquipmentItems"
                ("Id", "PlayerId", "ServerId", "ItemKey", "Slot", "SlotIndex", "Rarity",
                 "EnhancementLevel", "IsBroken", "EquippedByHeroId", "AcquiredAt", "UpdatedAt")
            VALUES
                ({id}, {Guid.NewGuid()}, 1, {itemKey}, {slot}, {slotIndex}, 1,
                 0, false, {equippedBy}, now(), now())
            """);

        return id;
    }

    private static async Task<List<Row>> ReadAsync(AppDbContext context)
        => await context.Database
            .SqlQuery<Row>($"""
                SELECT "Id", "ItemKey", "EquippedByHeroId", "SlotIndex" FROM "EquipmentItems"
                """)
            .ToListAsync();
}
