using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Exceptions;

/// <summary>
/// Причина відмови з параметрами — контракт із текстом на клієнті.
/// Параметр, що загубився дорогою, дав би гравцю «відкриється на рівні {level}».
/// </summary>
public class RefusalReasonTests
{
    private static readonly RefusalReason LevelLocked = new("test.levelLocked", "level", "building");

    [Fact]
    public void Exception_ShouldCarryTheReasonAndNamedArgs()
    {
        var exception = new RequirementNotMetException(LevelLocked, "Requires town hall 5.", 5, "Ратуша");

        Assert.Equal("test.levelLocked", exception.Reason);
        Assert.Equal(5, exception.Args["level"]);
        Assert.Equal("Ратуша", exception.Args["building"]);
        Assert.Equal("Requires town hall 5.", exception.Message);
    }

    /// <summary>Розбіжність кількості — баг у місці кидка: краще впасти в тесті, ніж показати гравцю дірку в тексті.</summary>
    [Fact]
    public void Exception_WithTooFewValues_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new InvalidStateException(LevelLocked, "Requires town hall 5.", 5));
    }

    /// <summary>Відмова без причини — з багу клієнта, а не з гри: гравець побачить нейтральний текст.</summary>
    [Fact]
    public void Exception_WithoutReason_ShouldHaveNoReasonAndNoArgs()
    {
        var exception = new RequirementNotMetException("Weapon 'x' has no price.");

        Assert.Null(exception.Reason);
        Assert.Empty(exception.Args);
    }
}
