using EmpireIdle.Domain.Entities;

namespace EmpireIdle.API.DTOs
{
    /// <summary>
    /// Запит на відправлення армії: тип цілі, її id, склад військ і намір.
    /// Намір за замовчуванням — атака: старі клієнти його не надсилають.
    /// </summary>
    public record SendMarchRequest(
        MarchTargetType TargetType,
        Guid TargetId,
        Dictionary<string, int> Units,
        MarchIntent Intent = MarchIntent.Attack);
}
