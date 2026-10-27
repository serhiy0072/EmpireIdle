namespace EmpireIdle.Application.Beasts.ReadModels
{
    /// <summary>Монстр на мапі, якого можна піти приручити.</summary>
    public record TameTargetView(Guid MonsterId, string MonsterKey, int Level, int X, int Y);
}
