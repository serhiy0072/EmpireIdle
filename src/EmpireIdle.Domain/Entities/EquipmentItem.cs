namespace EmpireIdle.Domain.Entities
{
    /// <summary>Слот, у який вдягається спорядження.</summary>
    public enum EquipmentSlot
    {
        Weapon = 1,
        Armor = 2,
        Helmet = 3,
        Boots = 4,
        Accessory = 5
    }

    /// <summary>
    /// Унікальний екземпляр спорядження з власними характеристиками.
    /// На відміну від стакових предметів, кожен меч — окремий запис.
    /// </summary>
    public class EquipmentItem : Entity
    {
        private readonly List<EquipmentStat> _stats = new();

        public Guid PlayerId { get; private set; }

        /// <summary>Ключ базового типу з конфіга (наприклад "sword_iron").</summary>
        public string ItemKey { get; private set; } = null!;

        /// <summary>Слот, у який вдягається.</summary>
        public EquipmentSlot Slot { get; private set; }

        /// <summary>common / rare / legendary — впливає на силу статів.</summary>
        public string Rarity { get; private set; } = null!;

        /// <summary>Рівень заточки (0 — не заточене).</summary>
        public int EnhancementLevel { get; private set; }

        /// <summary>Герой, на якому вдягнене; null — лежить в інвентарі.</summary>
        public Guid? EquippedByHeroId { get; private set; }

        /// <summary>Індивідуальні характеристики екземпляра.</summary>
        public IReadOnlyCollection<EquipmentStat> Stats => _stats.AsReadOnly();

        public DateTime AcquiredAt { get; private set; }

        public EquipmentItem(Guid id, Guid playerId, string itemKey, EquipmentSlot slot,
            string rarity, IEnumerable<(string Stat, double Value)> stats, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ItemKey = itemKey;
            Slot = slot;
            Rarity = rarity;
            EnhancementLevel = 0;
            AcquiredAt = utcNow;

            foreach (var (stat, value) in stats)
                _stats.Add(new EquipmentStat(Guid.NewGuid(), id, stat, value));
        }

        protected EquipmentItem() { } // Для EF Core

        /// <summary>Вдягає предмет на героя.</summary>
        public void EquipTo(Guid heroId)
        {
            if (EquippedByHeroId is not null)
                throw new InvalidOperationException($"Equipment {Id} is already equipped.");

            EquippedByHeroId = heroId;
        }

        /// <summary>Знімає предмет із героя.</summary>
        public void Unequip() => EquippedByHeroId = null;

        /// <summary>Підвищує рівень заточки.</summary>
        public void Enhance() => EnhancementLevel++;

        /// <summary>Сумарне значення стата з урахуванням заточки (+10% за рівень).</summary>
        public double GetStatValue(string statKey)
        {
            var stat = _stats.FirstOrDefault(s => s.StatKey == statKey);
            if (stat is null)
                return 0;

            return stat.Value * (1 + EnhancementLevel * 0.1);
        }
    }

    /// <summary>Одна характеристика екземпляра спорядження.</summary>
    public class EquipmentStat : Entity
    {
        public Guid EquipmentItemId { get; private set; }

        /// <summary>Ключ стата: Attack, Defense, Speed…</summary>
        public string StatKey { get; private set; } = null!;

        public double Value { get; private set; }

        public EquipmentStat(Guid id, Guid equipmentItemId, string statKey, double value) : base(id)
        {
            EquipmentItemId = equipmentItemId;
            StatKey = statKey;
            Value = value;
        }

        protected EquipmentStat() { } // Для EF Core
    }
}