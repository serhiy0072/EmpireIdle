using EmpireIdle.Application.Garrisons.Commands;

namespace EmpireIdle.API.DTOs
{
    public record HealWoundedRequest(Dictionary<string, int> Units, HealPaymentMethod Payment);
}
