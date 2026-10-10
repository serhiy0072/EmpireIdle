using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Inventory.Effects
{
    /// <summary>
    /// Банка енергії данжів (рішення 11.10.2026): додає енергію й понад повну шкалу. Сама шкала
    /// понад стелю не росте — банки накопичуються й витрачаються пачкою забігів.
    /// </summary>
    public class DungeonEnergyItemEffect : IItemEffect
    {
        public string ItemType => "energy";

        private readonly IDungeonRepository _dungeons;
        private readonly GameCatalog _catalog;

        public DungeonEnergyItemEffect(IDungeonRepository dungeons, GameCatalog catalog)
        {
            _dungeons = dungeons;
            _catalog = catalog;
        }

        public async Task ApplyAsync(ItemUsageContext context, CancellationToken cancellationToken)
        {
            var settings = _catalog.Config.Dungeons;
            var energy = await _dungeons.GetEnergyAsync(context.PlayerId, cancellationToken);

            if (energy is null)
            {
                // Як і на першому забігу: хто ще не заходив у данж, має повну шкалу
                energy = new DungeonEnergy(Guid.NewGuid(), context.PlayerId, settings.MaxEnergy, context.UtcNow);
                await _dungeons.AddEnergyAsync(energy, cancellationToken);
            }

            energy.AddBeyondCap(checked(context.Config.DungeonEnergy * context.Count), settings.MaxEnergy, settings.RegenHours,
                context.UtcNow);
        }
    }
}
