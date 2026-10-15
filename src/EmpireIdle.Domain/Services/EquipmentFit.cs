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
        private readonly ArtifactStats _stats;

        public EquipmentFit(GameCatalog catalog)
        {
            _catalog = catalog;
            _stats = new ArtifactStats(catalog);
        }

        /// <summary>
        /// Номер слота артефакта — за його типом (намисто, корона…). Артефакт без відомого типу
        /// валідатор не пропускає, тож тут це битий конфіг, а не помилка гравця.
        /// </summary>
        public int SlotIndexOf(ItemConfig itemConfig)
            => _catalog.Config.Equipment.ArtifactSlotIndex(itemConfig.ArtifactSlot)
               ?? throw new InvalidOperationException(
                   $"Artifact '{itemConfig.Key}' has no known ArtifactSlot '{itemConfig.ArtifactSlot}'.");

        /// <summary>Набір предмета для свого класу, або предмет поза класовими наборами.</summary>
        public bool FitsClass(ItemConfig itemConfig, string heroClass)
            => _catalog.Config.Equipment.FindArtifactSet(itemConfig.SetKey)?.Class is not { } setClass || setClass == heroClass;

        /// <summary>
        /// Чи кращий кандидат за те, що вже стоїть на герої цього класу: вища рідкість, далі більша сила
        /// під клас героя. Зламаний рахується зі своєю урізаною силою — і все одно може виявитися кращим.
        /// Рівний не кращий — автоекіп не міняє шило на швайку.
        /// </summary>
        public bool IsBetter(EquipmentItem candidate, EquipmentItem? current, string heroClass)
        {
            if (current is null)
                return true;

            if (candidate.Rarity != current.Rarity)
                return candidate.Rarity > current.Rarity;

            var multiplier = _stats.ClassMultiplier(heroClass);

            return _stats.Power(candidate, multiplier) > _stats.Power(current, multiplier);
        }

        /// <summary>
        /// Найкраще вільне для кожного слота героя цього класу: не вдягнене, не виставлене на ринок
        /// і з набору свого класу.
        /// Порядок той самий, що в <see cref="IsBetter"/>; за рівної сили — вищий рівень і заточка.
        /// Ключ — номер слота артефакта.
        /// </summary>
        public Dictionary<int, EquipmentItem> BestFree(IEnumerable<EquipmentItem> owned, string heroClass)
        {
            var multiplier = _stats.ClassMultiplier(heroClass);

            return owned
                .Where(e => e.EquippedByHeroId is null && !e.IsOnMarket)
                .Select(e => (Item: e, Config: _catalog.FindItem(e.ItemKey)))
                .Where(pair => pair.Config is not null && FitsClass(pair.Config, heroClass))
                .GroupBy(pair => SlotIndexOf(pair.Config!))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(pair => pair.Item)
                        .OrderByDescending(e => e.Rarity)
                        .ThenByDescending(e => _stats.Power(e, multiplier))
                        .ThenByDescending(e => e.Level)
                        .ThenByDescending(e => e.Mastery)
                        .ThenBy(e => e.Id)
                        .First());
        }
    }
}
