using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Marches.ReadModels
{
    /// <summary>
    /// Активний похід очима гравця. TargetName може бути null: монстра
    /// вже могли вбити, а село — покинути; марш при цьому ще повертається.
    /// TargetLevel — рівень монстра; у села й зниклої цілі null.
    /// </summary>
    public record MarchView(
        Guid Id,
        MarchTargetType TargetType,
        Guid TargetId,
        string? TargetName,
        int? TargetLevel,
        int TargetX,
        int TargetY,
        MarchIntent Intent,
        MarchState State,
        Guid? HeroId,
        DateTime DepartedAt,
        DateTime LegStartedAt,
        DateTime ArrivesAt,
        List<MarchUnitView> Units,
        int SpeedUpCostGems);

    /// <summary>Загін у поході: тип, рівень і скільки ще живих.</summary>
    public record MarchUnitView(string UnitType, int Level, int Count);
}
