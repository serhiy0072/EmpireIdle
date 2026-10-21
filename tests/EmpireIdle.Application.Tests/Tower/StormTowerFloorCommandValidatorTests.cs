using EmpireIdle.Application.Tower.Commands;
using EmpireIdle.Application.Tower.Validators;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;

namespace EmpireIdle.Application.Tests.Tower;

/// <summary>Загін штурму перевіряється за розміром із конфіга — тим самим, яким розставляє рушій.</summary>
public class StormTowerFloorCommandValidatorTests
{
    private const int SquadSize = 3;

    private readonly StormTowerFloorCommandValidator _validator;

    public StormTowerFloorCommandValidatorTests()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.RealtimeBattle.SquadSize = SquadSize;
        _validator = new StormTowerFloorCommandValidator(new GameCatalog(config));
    }

    private bool Valid(params Guid?[] lanes) => _validator.Validate(new StormTowerFloorCommand(Guid.NewGuid(), 1, lanes)).IsValid;

    [Fact]
    public void Validate_ShouldTakeAFullSquad() => Assert.True(Valid(Guid.NewGuid(), null, Guid.NewGuid()));

    [Fact]
    public void Validate_ShouldRejectMoreLanesThanTheSquadHas() => Assert.False(Valid(Guid.NewGuid(), null, null, null));

    [Fact]
    public void Validate_ShouldRejectAnEmptySquad() => Assert.False(Valid(null, null, null));

    [Fact]
    public void Validate_ShouldRejectAHeroOnTwoLanes()
    {
        var hero = Guid.NewGuid();

        Assert.False(Valid(hero, hero));
    }

    private bool ValidCasts(params ManualCast[] casts)
        => _validator.Validate(new StormTowerFloorCommand(Guid.NewGuid(), 1, [Guid.NewGuid()], casts)).IsValid;

    [Fact]
    public void Validate_ShouldTakeOrdersInsideTheBattle() => Assert.True(ValidCasts(new ManualCast(1, 0), new ManualCast(900, SquadSize - 1)));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(901, 0)]
    [InlineData(10, SquadSize)]
    [InlineData(10, -1)]
    public void Validate_ShouldRejectOrdersOutsideTheBattle(int tick, int lane) => Assert.False(ValidCasts(new ManualCast(tick, lane)));
}
