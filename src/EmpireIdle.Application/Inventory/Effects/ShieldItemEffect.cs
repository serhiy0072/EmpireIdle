using EmpireIdle.Application.Common;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>Щит села на строк предмета: той самий, що й після падіння міста, тож і правила ті самі.</summary>
    public class ShieldItemEffect : IItemEffect
    {
        public string ItemType => "shield";

        private readonly IVillageRepository _villages;

        public ShieldItemEffect(IVillageRepository villages) => _villages = villages;

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            var village = await _villages.RequireByPlayerAsync(context.PlayerId, cancellationToken);

            village.RaiseShield(TimeSpan.FromHours(context.Config.DurationHours * context.Count), context.UtcNow);
        }
    }
}
