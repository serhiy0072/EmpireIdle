namespace EmpireIdle.Domain.Services
{
    /// <summary>Скільки юнітів утратив конкретний стек оборони.</summary>
    public record StackLoss(Guid? OwnerPlayerId, string UnitType, int Lost);
}
