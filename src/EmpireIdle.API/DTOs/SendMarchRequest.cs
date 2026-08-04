using EmpireIdle.Domain.Entities;

namespace EmpireIdle.API.DTOs
{
    /// <summary>Запит на відправлення армії: тип цілі, її id і склад військ.</summary>
    public record SendMarchRequest(MarchTargetType TargetType, Guid TargetId, Dictionary<string, int> Units);
}