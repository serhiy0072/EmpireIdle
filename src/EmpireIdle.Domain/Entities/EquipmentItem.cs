using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Унікальний екземпляр артефакта. На відміну від стакових предметів,
    /// кожен — окремий запис із власними статами, рівнем і майстерністю (GDD §6.4).
    ///
    /// Прив'язаний до світу, як і герой: спорядження носить герой, а герої
    /// живуть у своєму сервері окремо.
    /// </summary>
    public class EquipmentItem : Entity
    {
        private readonly List<EquipmentStat> _stats = new();
        private readonly List<EquipmentRoll> _rolls = new();

        public Guid PlayerId { get; private set; }

        /// <summary>
        /// Світ, якому належить предмет. Дублює ServerId гравця навмисно:
        /// query-фільтр застосовується до кореня агрегату, а не через навігацію.
        /// </summary>
        public int ServerId { get; private set; }

        /// <summary>Ключ базового типу з конфіга (наприклад "dawn_necklace").</summary>
        public string ItemKey { get; private set; } = null!;

        /// <summary>Вид спорядження — артефакт (зброя — частина героя, GDD §6.4).</summary>
        public EquipmentSlot Slot { get; private set; }

        /// <summary>
        /// Номер слота артефакта: 0–3 за типом (намисто, корона…).
        /// Проставляється вдяганням, бо це властивість місця, а не предмета:
        /// той самий артефакт можна перевісити в інший слот.
        /// </summary>
        public int SlotIndex { get; private set; }

        /// <summary>Рідкість екземпляра — впливає на силу статів.</summary>
        public Rarity Rarity { get; private set; }

        /// <summary>
        /// Рівень артефакта (GDD §6.4): росте від досвіду згодованого спорядження й гаєчок.
        /// На ньому — ролли нових статів; кожен рівень додає частку до статів.
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// Увесь досвід, вкладений в артефакт, накопичено. Згодований артефакт передає його
        /// повністю — заміна на кращий не карає за вкладене.
        /// </summary>
        public long Experience { get; private set; }

        /// <summary>Майстерність коваля — заточка за золото з шансом, без поломки (GDD §6.4).</summary>
        public int Mastery { get; private set; }

        /// <summary>Герой, на якому вдягнене; null — лежить в інвентарі.</summary>
        public Guid? EquippedByHeroId { get; private set; }

        /// <summary>Індивідуальні характеристики екземпляра.</summary>
        public IReadOnlyCollection<EquipmentStat> Stats => _stats.AsReadOnly();

        /// <summary>Журнал роллів: рівень і сід кожного.</summary>
        public IReadOnlyCollection<EquipmentRoll> Rolls => _rolls.AsReadOnly();

        public DateTime AcquiredAt { get; private set; }

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        /// <summary>
        /// Предмет у заставі ринку: його не можна вдягнути, прокачати
        /// чи виставити вдруге, доки лот не закриється.
        /// </summary>
        public bool IsOnMarket { get; private set; }

        /// <summary>Куплений на ринку предмет не виставляється знову до цього моменту.</summary>
        public DateTime? ResaleLockedUntil { get; private set; }

        public EquipmentItem(Guid id, Guid playerId, int serverId, string itemKey, EquipmentSlot slot,
            Rarity rarity, IEnumerable<(string Stat, double Value)> stats, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            ItemKey = itemKey;
            Slot = slot;
            Rarity = rarity;
            Level = 0;
            AcquiredAt = utcNow;
            UpdatedAt = utcNow;

            foreach (var (stat, value) in stats)
                _stats.Add(new EquipmentStat(Guid.NewGuid(), id, stat, value));
        }

        protected EquipmentItem() { } // Для EF Core

        /// <summary>
        /// Вдягає предмет на героя в заданий слот.
        ///
        /// Що слот вільний і що артефакт не дублює набір, вирішує не цей
        /// метод, а частковий унікальний індекс: два паралельні вдягання
        /// інакше дали б героєві дві зброї.
        /// </summary>
        public void EquipTo(Guid heroId, int slotIndex, DateTime utcNow)
        {
            EnsureNotOnMarket();

            if (EquippedByHeroId is not null)
                throw new InvalidStateException($"Equipment {Id} is already equipped.");

            EquippedByHeroId = heroId;
            SlotIndex = slotIndex;
            Touch(utcNow);
            RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>Знімає предмет із героя.</summary>
        public void Unequip(DateTime utcNow)
        {
            EquippedByHeroId = null;
            SlotIndex = 0;
            Touch(utcNow);
            RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>
        /// Додає досвід згодованого спорядження й гаєчок і ставить рівень, який він дає.
        /// Криву й стелю знає конфіг, тож рівень рахує викликач; ролли нових рівнів — теж він.
        /// </summary>
        public void GainExperience(long amount, int newLevel, DateTime utcNow)
        {
            EnsureNotOnMarket();

            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Experience only grows.");

            if (newLevel < Level)
                throw new ArgumentOutOfRangeException(nameof(newLevel), newLevel, "An artifact never loses levels.");

            Experience += amount;
            Level = newLevel;
            Touch(utcNow);

            // Сила рахується лише з вдягнутого: прокачка на складі її не рухає
            if (EquippedByHeroId is not null)
                RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>Підвищує майстерність. Стелю й кидок шансу знає викликач.</summary>
        public void RaiseMastery(DateTime utcNow)
        {
            EnsureNotOnMarket();

            Mastery++;
            Touch(utcNow);

            if (EquippedByHeroId is not null)
                RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>
        /// Чи можна згодувати цей предмет: не вдягнутий і не в заставі ринку.
        /// Рідкість (унікальні не годуються) — правило конфіга, його перевіряє викликач.
        /// </summary>
        public void EnsureCanBeFed()
        {
            EnsureNotOnMarket();

            if (EquippedByHeroId is not null)
                throw new InvalidStateException(RefusalReasons.EquipmentFoodEquipped,
                    $"Equipment {Id} is worn by a hero and cannot be fed.");
        }

        /// <summary>
        /// Додає новий стат. Артефакти набирають їх на 4 і 8 рівнях:
        /// які саме — вирішує ролер, предмет лише зберігає результат.
        /// </summary>
        public void AddStat(string statKey, double value, DateTime utcNow)
        {
            if (_stats.Any(s => s.StatKey == statKey))
                throw new AlreadyExistsException("Equipment stat", statKey);

            _stats.Add(new EquipmentStat(Guid.NewGuid(), Id, statKey, value));
            Touch(utcNow);

            // Сила рахується лише з вдягнутого: заточка на складі її не рухає
            if (EquippedByHeroId is not null)
                RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>Підсилює наявний стат на задану величину.</summary>
        public void RaiseStat(string statKey, double delta, DateTime utcNow)
        {
            var stat = _stats.FirstOrDefault(s => s.StatKey == statKey)
                ?? throw new EntityNotFoundException("Equipment stat", statKey);

            stat.Raise(delta);
            Touch(utcNow);

            // Сила рахується лише з вдягнутого: заточка на складі її не рухає
            if (EquippedByHeroId is not null)
                RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>
        /// Значення стата з рівнем і майстерністю (GDD §9.12): база × (1 + бонус рівня + бонус майстерності).
        /// Бонуси складаються, а не множаться — разом вони дають ту саму стелю, що й стара заточка.
        /// </summary>
        /// <param name="levelBonus">Приріст за рівень артефакта, часткою; з конфіга — криву балансують.</param>
        /// <param name="masteryBonus">Приріст за рівень майстерності, часткою.</param>
        public double GetStatValue(string statKey, double levelBonus, double masteryBonus)
        {
            var stat = _stats.FirstOrDefault(s => s.StatKey == statKey);

            if (stat is null)
                return 0;

            return stat.Value * (1 + Level * levelBonus + Mastery * masteryBonus);
        }

        /// <summary>
        /// Виставляє предмет на ринок. Вдягнений знімається тут же: продаж
        /// не має тримати героя з порожнім слотом у невизначеному стані.
        /// </summary>
        public void PutOnMarket(DateTime utcNow)
        {
            if (IsOnMarket)
                throw new InvalidStateException(RefusalReasons.MarketItemListed, $"Equipment {Id} is already on the market.");

            if (ResaleLockedUntil is { } until && until > utcNow)
                throw new RequirementNotMetException(RefusalReasons.MarketResaleCooldown,
                    $"Equipment {Id} was bought recently and cannot be resold until {until:O}.", until);

            if (EquippedByHeroId is not null)
                Unequip(utcNow);

            IsOnMarket = true;
            Touch(utcNow);
        }

        /// <summary>Лот знято або строк минув — предмет повертається в інвентар продавця.</summary>
        public void TakeOffMarket(DateTime utcNow)
        {
            if (!IsOnMarket)
                throw new InvalidStateException($"Equipment {Id} is not on the market.");

            IsOnMarket = false;
            Touch(utcNow);
        }

        /// <summary>Продаж: предмет переходить до покупця з кулдауном перепродажу.</summary>
        public void SellTo(Guid buyerId, DateTime resaleLockedUntil, DateTime utcNow)
        {
            if (!IsOnMarket)
                throw new InvalidStateException($"Equipment {Id} is not on the market.");

            var sellerId = PlayerId;

            PlayerId = buyerId;
            IsOnMarket = false;
            ResaleLockedUntil = resaleLockedUntil;
            Touch(utcNow);

            RaiseDomainEvent(new EquipmentChanged(sellerId, Id, utcNow));
            RaiseDomainEvent(new EquipmentChanged(buyerId, Id, utcNow));
        }

        /// <summary>
        /// Предмет у заставі ринку: будь-яка дія з ним — відмова. Публічний, бо майстерність
        /// мусить перевірити це до списання золота, а не після кидка.
        /// </summary>
        public void EnsureNotOnMarket()
        {
            if (IsOnMarket)
                throw new InvalidStateException(RefusalReasons.MarketItemListed, $"Equipment {Id} is on the market.");
        }

        /// <summary>Записує ролл у журнал, щоб його можна було переграти.</summary>
        public void RecordRoll(int level, int seed, DateTime utcNow)
            => _rolls.Add(new EquipmentRoll(Guid.NewGuid(), Id, level, seed, utcNow));

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
    }
}
