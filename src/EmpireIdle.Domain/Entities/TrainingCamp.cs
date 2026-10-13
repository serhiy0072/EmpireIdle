using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Навчальний табір гравця (GDD §6.1, рішення 07.10.2026): докуплені слоти й перезарядка
    /// звільнених. Хто стоїть у слоті, тримає сам герой (Hero.CampSlot) — одне джерело правди,
    /// а гонку двох героїв за один слот розв'язує унікальний індекс.
    /// </summary>
    public class TrainingCamp : Entity
    {
        public Guid PlayerId { get; private set; }
        public int ServerId { get; private set; }

        /// <summary>Скільки слотів докуплено за gems понад безкоштовні.</summary>
        public int PurchasedSlots { get; private set; }

        /// <summary>Звільнені слоти, що ще перезаряджаються: номер слота → до якого моменту.</summary>
        public IReadOnlyDictionary<int, DateTime> SlotCooldowns { get; private set; } = new Dictionary<int, DateTime>();

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public TrainingCamp(Guid id, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            UpdatedAt = utcNow;
        }

        protected TrainingCamp() { } // Для EF Core

        public bool IsCoolingDown(int slot, DateTime utcNow)
            => SlotCooldowns.TryGetValue(slot, out var until) && until > utcNow;

        /// <summary>
        /// Гравець обрав слот — він має бути відкритим, вільним і не на перезарядці.
        /// Хто стоїть у слотах, знає ростер, тож зайняті передає викликач.
        /// </summary>
        public void EnsureSlotFree(int slot, int totalSlots, IReadOnlySet<int> occupied, DateTime utcNow)
        {
            if (slot < 0 || slot >= totalSlots)
                throw new RequirementNotMetException(RefusalReasons.CampSlotLocked,
                    $"Camp slot {slot} is not open — the player has {totalSlots}.", slot + 1);

            if (occupied.Contains(slot))
                throw new RequirementNotMetException(RefusalReasons.CampSlotTaken, $"Camp slot {slot} is taken.", slot + 1);

            if (IsCoolingDown(slot, utcNow))
                throw new RequirementNotMetException(RefusalReasons.CampSlotCooling,
                    $"Camp slot {slot} is cooling down.", slot + 1, SlotCooldowns[slot]);
        }

        /// <summary>Героя вийняли — слот перезаряджається, щоб табір не став безкоштовною ротацією героїв.</summary>
        public void StartCooldown(int slot, TimeSpan cooldown, DateTime utcNow)
        {
            SlotCooldowns = new Dictionary<int, DateTime>(SlotCooldowns) { [slot] = utcNow + cooldown };
            UpdatedAt = utcNow;
        }

        /// <summary>Знімає перезарядку слота. Gems списує викликач — гаманець поза агрегатом.</summary>
        public void SkipCooldown(int slot, DateTime utcNow)
        {
            if (!IsCoolingDown(slot, utcNow))
                throw new RequirementNotMetException(RefusalReasons.CampSlotReady, $"Camp slot {slot} is not cooling down.", slot + 1);

            var cooldowns = new Dictionary<int, DateTime>(SlotCooldowns);
            cooldowns.Remove(slot);

            SlotCooldowns = cooldowns;
            UpdatedAt = utcNow;
        }

        /// <summary>Докуповує слот. Ціну рахує й списує викликач.</summary>
        public void AddPurchasedSlot(int maxPurchasable, DateTime utcNow)
        {
            if (PurchasedSlots >= maxPurchasable)
                throw new RequirementNotMetException(RefusalReasons.CampAllSlotsBought,
                    $"All {maxPurchasable} extra camp slots are already bought.", maxPurchasable);

            PurchasedSlots++;
            UpdatedAt = utcNow;
        }
    }
}
