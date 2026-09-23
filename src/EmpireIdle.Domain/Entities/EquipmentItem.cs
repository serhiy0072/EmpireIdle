using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Унікальний екземпляр спорядження. На відміну від стакових предметів,
    /// кожен меч — окремий запис із власними статами й заточкою.
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

        /// <summary>Ключ базового типу з конфіга (наприклад "sword_iron").</summary>
        public string ItemKey { get; private set; } = null!;

        /// <summary>Зброя чи артефакт.</summary>
        public EquipmentSlot Slot { get; private set; }

        /// <summary>
        /// Номер слота в межах типу: 0 для зброї, 0–3 для артефактів.
        /// Проставляється вдяганням, бо це властивість місця, а не предмета:
        /// той самий артефакт можна перевісити в інший слот.
        /// </summary>
        public int SlotIndex { get; private set; }

        /// <summary>Рідкість екземпляра — впливає на силу статів.</summary>
        public Rarity Rarity { get; private set; }

        /// <summary>Рівень заточки (0 — не заточене).</summary>
        public int EnhancementLevel { get; private set; }

        /// <summary>
        /// Зламана зброя. Заточка може провалитись і зіпсувати предмет:
        /// він лишається в інвентарі, але не вдягається, поки не полагоджений.
        /// Артефакти не ламаються — вони й здобуваються інакше.
        /// </summary>
        public bool IsBroken { get; private set; }

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

        public EquipmentItem(Guid id, Guid playerId, int serverId, string itemKey, EquipmentSlot slot,
            Rarity rarity, IEnumerable<(string Stat, double Value)> stats, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            ItemKey = itemKey;
            Slot = slot;
            Rarity = rarity;
            EnhancementLevel = 0;
            AcquiredAt = utcNow;
            UpdatedAt = utcNow;

            foreach (var (stat, value) in stats)
                _stats.Add(new EquipmentStat(Guid.NewGuid(), id, stat, value));
        }

        protected EquipmentItem() { } // Для EF Core

        /// <summary>Чи можна вдягнути просто зараз.</summary>
        public bool IsWearable => !IsBroken && EquippedByHeroId is null;

        /// <summary>
        /// Вдягає предмет на героя в заданий слот.
        ///
        /// Що слот вільний і що артефакт не дублює набір, вирішує не цей
        /// метод, а частковий унікальний індекс: два паралельні вдягання
        /// інакше дали б героєві дві зброї.
        /// </summary>
        public void EquipTo(Guid heroId, int slotIndex, DateTime utcNow)
        {
            if (EquippedByHeroId is not null)
                throw new InvalidStateException($"Equipment {Id} is already equipped.");

            if (IsBroken)
                throw new InvalidStateException(RefusalReasons.EquipmentBroken, $"Equipment {Id} is broken and must be repaired first.");

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
        /// Підвищує рівень заточки. Стелю перевіряє викликач: вона залежить
        /// від конфіга, про який предмет не знає.
        /// </summary>
        public void Enhance(DateTime utcNow)
        {
            if (IsBroken)
                throw new InvalidStateException($"Equipment {Id} is broken.");

            EnhancementLevel++;
            Touch(utcNow);

            // Сила рахується лише з вдягнутого: заточка на складі її не рухає
            if (EquippedByHeroId is not null)
                RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>
        /// Предмет зіпсовано. Рівень при цьому не падає: гравець утратив
        /// спробу, а не прогрес.
        ///
        /// Слот тут не перевіряється: те, що артефакти не ламаються, —
        /// правило заточки, і живе воно в EnhanceArtifactCommand, який
        /// просто ніколи цього не кличе.
        /// </summary>
        public void Break(DateTime utcNow)
        {
            var wasEquipped = EquippedByHeroId is not null;

            IsBroken = true;
            EquippedByHeroId = null;
            SlotIndex = 0;
            Touch(utcNow);

            if (wasEquipped)
                RaiseDomainEvent(new EquipmentChanged(PlayerId, Id, utcNow));
        }

        /// <summary>Лагодить зброю. Ціну списує викликач.</summary>
        public void Repair(DateTime utcNow)
        {
            if (!IsBroken)
                throw new InvalidStateException($"Equipment {Id} is not broken.");

            // Зламане завжди зняте, тож ремонт сили не міняє — події немає
            IsBroken = false;
            Touch(utcNow);
        }

        /// <summary>
        /// Додає новий стат. Артефакти набирають їх на 4 і 8 рівнях заточки:
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
        /// Значення стата з урахуванням заточки.
        /// </summary>
        /// <param name="enhancementBonus">
        /// Приріст за рівень заточки, часткою. Береться з конфіга, а не
        /// зашитий: криву заточки балансують, і вона не властивість предмета.
        /// </param>
        public double GetStatValue(string statKey, double enhancementBonus)
        {
            var stat = _stats.FirstOrDefault(s => s.StatKey == statKey);

            if (stat is null || IsBroken)
                return 0;

            return stat.Value * (1 + EnhancementLevel * enhancementBonus);
        }
        /// <summary>Записує ролл у журнал, щоб його можна було переграти.</summary>
        public void RecordRoll(int level, int seed, DateTime utcNow)
            => _rolls.Add(new EquipmentRoll(Guid.NewGuid(), Id, level, seed, utcNow));

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
    }
}
