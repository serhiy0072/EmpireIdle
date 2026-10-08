using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Куди стає спорядження і що краще (GDD §6.1). Спільне для ручного вдягання й «швидкого
    /// використання»: розійдись ці правила — автоекіп вдягав би те, що руками вдягнути не можна.
    /// </summary>
    public class EquipmentFit
    {
        private readonly GameCatalog _catalog;

        public EquipmentFit(GameCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Зброя завжди в нульовому слоті, артефакт — у слоті свого типу.
        /// Артефакт без відомого типу валідатор не пропускає, тож тут це
        /// битий конфіг, а не помилка гравця.
        /// </summary>
        public int SlotIndexOf(EquipmentSlot slot, ItemConfig itemConfig)
        {
            if (slot == EquipmentSlot.Weapon)
                return 0;

            return _catalog.Config.Equipment.ArtifactSlotIndex(itemConfig.ArtifactSlot)
                ?? throw new InvalidOperationException(
                    $"Artifact '{itemConfig.Key}' has no known ArtifactSlot '{itemConfig.ArtifactSlot}'.");
        }

        /// <summary>
        /// Зброя підходить за класом героя. Порожній список класів у конфігу
        /// означає «підходить усім», а не «нікому»: більшість артефактів
        /// саме такі.
        /// </summary>
        public bool Fits(string? heroClass, EquipmentSlot slot, ItemConfig itemConfig)
            => slot != EquipmentSlot.Weapon
               || itemConfig.WeaponClasses.Count == 0
               || (heroClass is not null && itemConfig.WeaponClasses.Contains(heroClass));

        /// <summary>
        /// Чи кращий кандидат за те, що вже стоїть: вища рідкість, за рівної — більша заточка.
        /// Рівний не кращий — автоекіп не міняє шило на швайку.
        /// </summary>
        public static bool IsBetter(EquipmentItem candidate, EquipmentItem? current)
            => current is null
               || candidate.Rarity > current.Rarity
               || (candidate.Rarity == current.Rarity && candidate.EnhancementLevel > current.EnhancementLevel);

        /// <summary>
        /// Найкраще вільне для кожного слота героя: не вдягнене, не зламане, не виставлене на ринок
        /// і придатне для його класу. Ключ — (тип слота, номер слота).
        /// </summary>
        public Dictionary<(EquipmentSlot Slot, int Index), EquipmentItem> BestFree(
            string? heroClass, IEnumerable<EquipmentItem> owned)
            => owned
                .Where(e => e.EquippedByHeroId is null && !e.IsBroken && !e.IsOnMarket)
                .Select(e => (Item: e, Config: _catalog.FindItem(e.ItemKey)))
                .Where(pair => pair.Config is not null && Fits(heroClass, pair.Item.Slot, pair.Config))
                .GroupBy(pair => (pair.Item.Slot, Index: SlotIndexOf(pair.Item.Slot, pair.Config!)))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(pair => pair.Item)
                        .OrderByDescending(e => e.Rarity)
                        .ThenByDescending(e => e.EnhancementLevel)
                        .ThenBy(e => e.Id)
                        .First());
    }
}
