using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Стан села, виведений із конфіга й рівнів: щит, відкритість будівель,
    /// бонус укріплень. Нічого не зберігається — все розсинхронізувалося б
    /// із конфігом при першому ребалансі.
    /// </summary>
    public sealed class VillageStatus
    {
        private readonly GameCatalog _catalog;

        public VillageStatus(GameCatalog catalog) => _catalog = catalog;

        /// <summary>Рівень ратуші; 0, якщо її чомусь немає.</summary>
        public int MainBuildingLevel(Village village)
            => village.Buildings.FirstOrDefault(b =>
                _catalog.Buildings.TryGetValue(b.Type, out var config) && config.IsMainBuilding)?.Level.Value ?? 0;

        /// <summary>
        /// Щит новачка: гравець нижче порогу не атакує, і його не атакують.
        /// Таймера немає — щит спадає рівнем ратуші, а вище за неї
        /// нічого не піднімеш (§3.2).
        /// </summary>
        public bool IsShielded(Village village)
            => MainBuildingLevel(village) < _catalog.Config.Combat.NewbieShieldTownHallLevel;

        /// <summary>
        /// Чи відкрита будівля гравцю. Під туманом вона існує й може
        /// будуватись, але гравець її не бачить.
        /// </summary>
        public bool IsUnlocked(Village village, string buildingType)
            => _catalog.Buildings.TryGetValue(buildingType, out var config)
               && config.RequiresMainBuildingLevel <= MainBuildingLevel(village);

        /// <summary>
        /// Множник до сили оборони від укріплень. 1.0 — стін немає.
        /// Належить селищу, тож підкріплення клану ними теж прикриті.
        /// </summary>
        public double DefenceMultiplier(Village village)
            => 1.0 + village.Buildings
                .Where(b => !b.IsUnderConstruction
                            && _catalog.Buildings.TryGetValue(b.Type, out var c)
                            && c.DefenceBonusPerLevel > 0)
                .Sum(b => _catalog.Buildings[b.Type].DefenceBonusPerLevel * b.Level.Value);
    }
}
