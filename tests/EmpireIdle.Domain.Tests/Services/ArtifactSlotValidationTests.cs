using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Типізовані артефактні слоти: намисто, корона, кільце, пояс.
/// Кожен тест ламає одну річ у валідному конфігу спорядження.
/// </summary>
public class ArtifactSlotValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithHeroes().WithEquipment().Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldAcceptTypedSlots()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    [Fact]
    public void Validate_ShouldRejectAnEmptySlotList()
        => Rejects(c => c.Equipment.ArtifactSlots.Clear());

    [Fact]
    public void Validate_ShouldRejectDuplicateSlotTypes()
        => Rejects(c => c.Equipment.ArtifactSlots.Add(new ArtifactSlotConfig { Key = "ring", DisplayName = "Ще кільце" }));

    /// <summary>Артефакт без типу слота нікуди не вдягнути.</summary>
    [Fact]
    public void Validate_ShouldRejectAnArtifactWithoutASlotType()
        => Assert.Contains(TestKeys.Artifact,
            Rejects(c => c.Items.Single(i => i.Key == TestKeys.Artifact).ArtifactSlot = null).Message);

    [Fact]
    public void Validate_ShouldRejectAnArtifactWithAnUnknownSlotType()
        => Assert.Contains("earring",
            Rejects(c => c.Items.Single(i => i.Key == TestKeys.Artifact).ArtifactSlot = "earring").Message);

    /// <summary>
    /// Дві частини набору в одному слоті разом не вдягнути: набір із чотирьох
    /// частин на трьох різних слотах недосяжний.
    /// </summary>
    [Fact]
    public void Validate_ShouldRejectASetWhosePiecesShareASlot()
        => Assert.Contains("distinct artifact slots",
            Rejects(c => c.Items.Single(i => i.Key == TestKeys.FourthArtifact).ArtifactSlot = "ring").Message);

    /// <summary>Номер слота — позиція типу в списку.</summary>
    [Fact]
    public void ArtifactSlotIndex_ShouldFollowTheListOrder()
    {
        var equipment = Valid().Equipment;

        Assert.Equal(0, equipment.ArtifactSlotIndex("necklace"));
        Assert.Equal(3, equipment.ArtifactSlotIndex("belt"));
        Assert.Null(equipment.ArtifactSlotIndex("earring"));
        Assert.Null(equipment.ArtifactSlotIndex(null));
    }
}
