using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Куди стає артефакт і що краще (GDD §6.4). Спільне для ручного вдягання й «швидкого
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
        /// Номер слота артефакта — за його типом (намисто, корона…). Артефакт без відомого типу
        /// валідатор не пропускає, тож тут це битий конфіг, а не помилка гравця.
        /// </summary>
        public int SlotIndexOf(ItemConfig itemConfig)
            => _catalog.Config.Equipment.ArtifactSlotIndex(itemConfig.ArtifactSlot)
               ?? throw new InvalidOperationException(
                   $"Artifact '{itemConfig.Key}' has no known ArtifactSlot '{itemConfig.ArtifactSlot}'.");

        /// <summary>
        /// Чи кращий кандидат за те, що вже стоїть: вища рідкість, за рівної — більша заточка.
        /// Рівний не кращий — автоекіп не міняє шило на швайку.
        /// </summary>
        public static bool IsBetter(EquipmentItem candidate, EquipmentItem? current)
            => current is null
               || candidate.Rarity > current.Rarity
               || (candidate.Rarity == current.Rarity && candidate.EnhancementLevel > current.EnhancementLevel);

        /// <summary>
        /// Найкраще вільне для кожного слота героя: не вдягнене, не зламане й не виставлене на ринок.
        /// Ключ — номер слота артефакта.
        /// </summary>
        public Dictionary<int, EquipmentItem> BestFree(IEnumerable<EquipmentItem> owned)
            => owned
                .Where(e => e.EquippedByHeroId is null && !e.IsBroken && !e.IsOnMarket)
                .Select(e => (Item: e, Config: _catalog.FindItem(e.ItemKey)))
                .Where(pair => pair.Config is not null)
                .GroupBy(pair => SlotIndexOf(pair.Config!))
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
