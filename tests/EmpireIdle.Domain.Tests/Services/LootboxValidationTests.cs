using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Скрині (GDD §9.12): таблиця на кожен предмет-скриню, відомі ключі, гарантія й вкладення без циклу.
/// Кожен тест ламає одну річ у валідному конфігу.
/// </summary>
public class LootboxValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithLootboxes().Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    private static LootboxConfig Box(GameConfig config, string key) => config.Lootboxes.Single(b => b.Key == key);

    [Fact]
    public void Validate_ShouldAcceptTheLootboxes()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    [Fact]
    public void Validate_ShouldRejectALootboxItemWithoutATable()
        => Assert.Contains(TestKeys.SmallChest,
            Rejects(c =>
            {
                c.Lootboxes.RemoveAll(b => b.Key == TestKeys.SmallChest);
                Box(c, TestKeys.BigChest).Entries.RemoveAll(e => e.IsLootbox);
            }).Message);

    [Fact]
    public void Validate_ShouldRejectATableWithoutALootboxItem()
        => Assert.Contains("ghost_chest",
            Rejects(c => c.Lootboxes.Add(new LootboxConfig
            {
                Key = "ghost_chest",
                Entries = [new LootboxEntryConfig { Type = "Gems", Amount = 1, Weight = 1 }]
            })).Message);

    [Fact]
    public void Validate_ShouldRejectDuplicateTables()
        => Rejects(c => c.Lootboxes.Add(new LootboxConfig
        {
            Key = TestKeys.SmallChest,
            Entries = [new LootboxEntryConfig { Type = "Gems", Amount = 1, Weight = 1 }]
        }));

    [Fact]
    public void Validate_ShouldRejectAnEmptyTable()
        => Assert.Contains(TestKeys.SmallChest, Rejects(c => Box(c, TestKeys.SmallChest).Entries.Clear()).Message);

    [Fact]
    public void Validate_ShouldRejectAZeroWeight()
        => Assert.Contains(TestKeys.SmallChest, Rejects(c => Box(c, TestKeys.SmallChest).Entries[0].Weight = 0).Message);

    [Fact]
    public void Validate_ShouldRejectAZeroAmount()
        => Assert.Contains(TestKeys.SmallChest, Rejects(c => Box(c, TestKeys.SmallChest).Entries[1].Amount = 0).Message);

    [Fact]
    public void Validate_ShouldRejectAnUnknownResource()
        => Assert.Contains("mithril", Rejects(c => Box(c, TestKeys.SmallChest).Entries[0].Key = "mithril").Message);

    /// <summary>Спорядження як Item ліг би стаковим рядком, який ніколи не стане предметом.</summary>
    [Fact]
    public void Validate_ShouldRejectEquipmentAsAnItem()
        => Assert.Contains(Piece,
            Rejects(c => Box(c, TestKeys.SmallChest).Entries.Add(new LootboxEntryConfig { Type = "Item", Key = Piece, Amount = 1, Weight = 1 }))
                .Message);

    private const string Piece = $"{TestKeys.DungeonSetKey}_common_necklace";

    [Fact]
    public void Validate_ShouldRejectAnUnknownRarityOfARandomArtifact()
        => Assert.Contains("Mythic", Rejects(c => Box(c, TestKeys.BigChest).Entries[2].Key = "Mythic").Message);

    /// <summary>Рідкість, якої немає в жодному наборі данжу, не видала б нічого.</summary>
    [Fact]
    public void Validate_ShouldRejectARandomArtifactWithoutDungeonPieces()
        => Rejects(c => c.Dungeons.Dungeons.Clear());

    [Fact]
    public void Validate_ShouldRejectAnUnknownNestedLootbox()
        => Assert.Contains("ghost_chest", Rejects(c => Box(c, TestKeys.BigChest).Entries[0].Key = "ghost_chest").Message);

    /// <summary>Вкладена скриня відкривається одразу — цикл не закінчився б ніколи.</summary>
    [Fact]
    public void Validate_ShouldRejectACycleOfNestedLootboxes()
        => Assert.Contains("contains itself",
            Rejects(c => Box(c, TestKeys.SmallChest).Entries.Add(new LootboxEntryConfig
            {
                Type = LootboxEntryConfig.LootboxType, Key = TestKeys.BigChest, Amount = 1, Weight = 1
            })).Message);

    [Fact]
    public void Validate_ShouldRejectPityWithoutAPityEntry()
        => Assert.Contains(TestKeys.BigChest, Rejects(c => Box(c, TestKeys.BigChest).Entries[2].Pity = false).Message);

    [Fact]
    public void Validate_ShouldRejectAPityEntryWithoutPity()
        => Assert.Contains(TestKeys.BigChest, Rejects(c => Box(c, TestKeys.BigChest).PityWithin = 0).Message);

    [Fact]
    public void Validate_ShouldRejectANegativePity()
        => Assert.Contains(TestKeys.BigChest, Rejects(c => Box(c, TestKeys.BigChest).PityWithin = -1).Message);

    /// <summary>Випадковий герой рідкості, якої немає серед героїв, не мав би кого видати.</summary>
    [Fact]
    public void Validate_ShouldRejectARandomHeroOfARarityWithoutHeroes()
        => Assert.Contains(TestKeys.SmallChest, Rejects(c => Box(c, TestKeys.SmallChest).Entries.Add(
            new LootboxEntryConfig { Type = LootboxEntryConfig.RandomHeroType, Key = "Rare", Amount = 1, Weight = 1 })).Message);

    /// <summary>Ключова скриня без таблиці або з ключем, що не є предметом-ключем, відкривалась би в нікуди.</summary>
    [Fact]
    public void Validate_ShouldRejectAKeyChestWithoutAKeyItem()
        => Assert.Contains(TestKeys.SmallChest, Rejects(c => c.KeyChests.Add(
            new KeyChestConfig { LootboxKey = TestKeys.SmallChest, KeyItemKey = TestKeys.BigChest, FreePerDay = 1 })).Message);

    [Fact]
    public void Validate_ShouldRejectAKeyChestWithoutATable()
        => Assert.Contains("missing_chest", Rejects(c => c.KeyChests.Add(
            new KeyChestConfig { LootboxKey = "missing_chest", KeyItemKey = TestKeys.BigChest })).Message);

    /// <summary>Відкриття без жодного кидка не дало б нічого.</summary>
    [Fact]
    public void Validate_ShouldRejectAChestWithoutRolls()
        => Assert.Contains(TestKeys.SmallChest, Rejects(c => Box(c, TestKeys.SmallChest).Rolls = 0).Message);
}
