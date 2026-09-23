using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Гарнізон села: юніти та черга тренування. Окремий агрегат —
    /// не знає про ресурси й вартість (це відповідальність Village/Application).
    /// </summary>
    public class Garrison : Entity
    {
        #region Стан

        private readonly List<VillageUnit> _units = new();
        private readonly List<UnitTrainingOrder> _trainingOrders = new();
        private readonly List<UnitLevelUpOrder> _levelUpOrders = new();
        private readonly List<WoundedUnit> _wounded = new();
        private readonly List<RecoverableUnit> _recoverable = new();
        private readonly List<ReinforcementUnit> _reinforcements = new();

        /// <summary>Село, якому належить гарнізон.</summary>
        public Guid VillageId { get; private set; }

        public IReadOnlyCollection<VillageUnit> Units => _units.AsReadOnly();
        public IReadOnlyCollection<UnitTrainingOrder> TrainingOrders => _trainingOrders.AsReadOnly();

        /// <summary>Активні замовлення прокачки (тільки для читання).</summary>
        public IReadOnlyCollection<UnitLevelUpOrder> LevelUpOrders => _levelUpOrders.AsReadOnly();

        /// <summary>Поранені в Госпіталі (тільки для читання).</summary>
        public IReadOnlyCollection<WoundedUnit> Wounded => _wounded.AsReadOnly();

        /// <summary>Юніти, доступні для викупу за gems (тільки для читання).</summary>
        public IReadOnlyCollection<RecoverableUnit> Recoverable => _recoverable.AsReadOnly();

        /// <summary>Чужі юніти, що стоять тут як підкріплення (тільки для читання).</summary>
        public IReadOnlyCollection<ReinforcementUnit> Reinforcements => _reinforcements.AsReadOnly();

        /// <summary>Скільки поранених зараз лежить у Госпіталі.</summary>
        public int WoundedCount => _wounded.Sum(w => w.Count);

        /// <summary>Скільки чужих юнітів зараз у гарнізоні.</summary>
        public int ReinforcementCount => _reinforcements.Sum(r => r.Count);

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>
        /// Світ, якому належить гарнізон. Дублює ServerId села навмисно:
        /// query-фільтр застосовується до кореня агрегату, а не через навігацію.
        /// </summary>
        public int ServerId { get; private set; }

        #endregion

        #region Створення

        public Garrison(Guid id, Guid villageId, int serverId) : base(id)
        {
            VillageId = villageId;
            ServerId = serverId;
        }

        protected Garrison() { } // Для EF Core

        #endregion

        #region Читання

        /// <summary>
        /// Склад оборони: власні юніти плюс підкріплення союзників.
        /// Поранені й ті, хто на прокачці, не беруть участі — їх тут немає.
        ///
        /// Повертає стеками, а не сумою: бій рахується на об'єднаній армії,
        /// але втрати потім треба повернути кожному власнику окремо.
        /// </summary>
        public IReadOnlyList<DefenceStack> GetDefence()
        {
            var own = _units
                .Where(u => u.Count > 0)
                .Select(u => new DefenceStack(null, u.UnitType, u.Level, u.Count));

            var allied = _reinforcements
                .Where(r => r.Count > 0)
                .Select(r => new DefenceStack(r.OwnerPlayerId, r.UnitType, r.Level, r.Count));

            return own.Concat(allied).ToList();
        }

        /// <summary>Хто тримає тут підкріплення — для екрана оборони й масового відкликання.</summary>
        public IReadOnlyCollection<Guid> ReinforcementOwners()
            => _reinforcements.Select(r => r.OwnerPlayerId).Distinct().ToList();

        /// <summary>Скільки юнітів зараз доступно для викупу.</summary>
        public int RecoverableCount(DateTime utcNow) => _recoverable.Where(r => r.IsActive(utcNow)).Sum(r => r.Count);

        /// <summary>
        /// Скільки місця в гарнізоні зайнято: свої юніти плюс усе, що тимчасово
        /// зняте на тренування чи прокачку. Марші й чужі підкріплення сюди
        /// не входять (§5.3, §5.2 GDD).
        /// </summary>
        private int Occupied()
            => _units.Sum(u => u.Count) + _trainingOrders.Sum(o => o.Count) + _levelUpOrders.Sum(o => o.Count);

        #endregion

        #region Тренування

        /// <summary>
        /// Ставить партію юнітів у чергу тренування.
        /// Інваріанти: розмір партії, одне активне замовлення, ліміт армії.
        /// </summary>
        /// <param name="level">
        /// Рівень, на якому юніти з'являться — тренування з нуля на обраний
        /// рівень, а не завжди на 1 (§5.2 GDD: "апати або створювати з нуля").
        /// </param>
        /// <param name="armyCapacity">
        /// Скільки юнітів гарнізон може тримати. Рахується від рівня казарм.
        /// Юніти в маршах у ліміт не входять: вони вже зняті з гарнізону, і
        /// перевіряти їх означало б тягнути в агрегат чужий стан. Чужі
        /// підкріплення теж не входять — у них свій ліміт від посольства.
        /// </param>
        public void TrainUnits(string unitType, int level, int count, int maxBatchSize, int armyCapacity,
            TimeSpan trainDuration, DateTime utcNow)
        {
            if (count < 1 || count > maxBatchSize)
                throw new RequirementNotMetException(RefusalReasons.GarrisonBatchSize,
                    $"Training batch size must be between 1 and {maxBatchSize}.", maxBatchSize);

            if (_trainingOrders.Any())
                throw new InvalidStateException(RefusalReasons.GarrisonTrainingBusy, "Barracks are already training a batch.");

            var occupied = Occupied();

            if (occupied + count > armyCapacity)
                throw new RequirementNotMetException(RefusalReasons.GarrisonArmyCapacity,
                    $"Army capacity exceeded: {occupied} of {armyCapacity} used, requested {count}.",
                    occupied, armyCapacity, count);

            _trainingOrders.Add(new UnitTrainingOrder(
                Guid.NewGuid(), Id, unitType, level, count, utcNow + trainDuration));
        }

        /// <summary>Завершує дозрілі замовлення: юніти йдуть у гарнізон.</summary>
        public int CompleteDueTraining(DateTime utcNow)
        {
            var due = _trainingOrders.Where(o => o.CompletesAt <= utcNow).ToList();

            foreach (var order in due)
            {
                var unit = _units.FirstOrDefault(u => u.UnitType == order.UnitType && u.Level == order.Level);
                if (unit is null)
                {
                    unit = new VillageUnit(Guid.NewGuid(), Id, order.UnitType, order.Level);
                    _units.Add(unit);
                }
                unit.Add(order.Count);
                _trainingOrders.Remove(order);
                RaiseDomainEvent(new Events.UnitsTrained(Id, VillageId, order.UnitType, order.Count, utcNow));
            }

            if (due.Count > 0)
                Touch(utcNow);

            return due.Count;
        }

        /// <summary>Прискорює замовлення тренування (speedup за gems).</summary>
        public void ReduceTrainingTime(Guid orderId, TimeSpan reduction, DateTime utcNow)
        {
            var order = _trainingOrders.FirstOrDefault(o => o.Id == orderId)
                 ?? throw new EntityNotFoundException("Training order", orderId);

            order.Reduce(reduction);
            Touch(utcNow);
        }

        #endregion

        #region Прокачка

        /// <summary>
        /// Ставить партію юнітів у чергу прокачки. Юніти знімаються з гарнізону
        /// одразу (§5.2 GDD): недоступні для маршів і оборони, поки не завершиться.
        /// Ліміту армії прокачка не підлягає — кількість не змінюється.
        /// </summary>
        public void LevelUpUnits(string unitType, int fromLevel, int toLevel, int count, int maxBatchSize,
            TimeSpan duration, DateTime utcNow)
        {
            if (count < 1 || count > maxBatchSize)
                throw new RequirementNotMetException(RefusalReasons.GarrisonBatchSize,
                    $"Level-up batch size must be between 1 and {maxBatchSize}.", maxBatchSize);

            if (toLevel <= fromLevel)
                throw new RequirementNotMetException("Target level must be higher than the current level.");

            if (_levelUpOrders.Any())
                throw new InvalidStateException(RefusalReasons.GarrisonLevelUpBusy, "Barracks are already levelling up a batch.");

            // Юніти — не ресурс: NotEnoughResources показав би гравцю «не вистачає swordsman»
            var available = _units.FirstOrDefault(u => u.UnitType == unitType && u.Level == fromLevel)?.Count ?? 0;

            if (available < count)
                throw new RequirementNotMetException(RefusalReasons.GarrisonNotEnoughUnits,
                    $"Only {available} '{unitType}' at level {fromLevel}, requested {count}.", count, available);

            var stack = _units.First(u => u.UnitType == unitType && u.Level == fromLevel);

            stack.Subtract(count);

            _levelUpOrders.Add(new UnitLevelUpOrder(
                Guid.NewGuid(), Id, unitType, fromLevel, toLevel, count, utcNow + duration));

            Touch(utcNow);
        }

        /// <summary>Завершує дозрілі прокачки: юніти повертаються в гарнізон на новому рівні.</summary>
        public int CompleteDueLevelUps(DateTime utcNow)
        {
            var due = _levelUpOrders.Where(o => o.CompletesAt <= utcNow).ToList();

            foreach (var order in due)
            {
                var unit = _units.FirstOrDefault(u => u.UnitType == order.UnitType && u.Level == order.ToLevel);
                if (unit is null)
                {
                    unit = new VillageUnit(Guid.NewGuid(), Id, order.UnitType, order.ToLevel);
                    _units.Add(unit);
                }
                unit.Add(order.Count);
                _levelUpOrders.Remove(order);
            }

            if (due.Count > 0)
                Touch(utcNow);

            return due.Count;
        }

        /// <summary>Прискорює замовлення прокачки (speedup за gems).</summary>
        public void ReduceLevelUpTime(Guid orderId, TimeSpan reduction, DateTime utcNow)
        {
            var order = _levelUpOrders.FirstOrDefault(o => o.Id == orderId)
                 ?? throw new EntityNotFoundException("Level-up order", orderId);

            order.Reduce(reduction);
            Touch(utcNow);
        }

        #endregion

        #region Марші

        /// <summary>
        /// Знімає юнітів із гарнізону для походу.
        /// </summary>
        /// <param name="units">Стек (тип+рівень) → кількість.</param>
        public void SendUnits(IReadOnlyDictionary<UnitStackKey, int> units, DateTime utcNow)
        {
            if (units.Count == 0)
                throw new RequirementNotMetException("Cannot send an empty army.");

            // Спершу перевіряємо ВСІ позиції — щоб не зняти частину і впасти
            foreach (var (stack, count) in units)
            {
                if (count < 1)
                    throw new RequirementNotMetException($"Invalid unit count for '{stack}'.");

                // Юніти — не ресурс: NotEnoughResources показав би гравцю «не вистачає swordsman».
                // Сюди приводить гонка — бій забрав частину загону, поки гравець заповнював форму
                var available = _units.FirstOrDefault(u => u.UnitType == stack.UnitType && u.Level == stack.Level)?.Count ?? 0;

                if (available < count)
                    throw new RequirementNotMetException(RefusalReasons.GarrisonNotEnoughUnits,
                        $"Only {available} of '{stack}' in the garrison, requested {count}.", count, available);
            }

            foreach (var (stack, count) in units)
                _units.First(u => u.UnitType == stack.UnitType && u.Level == stack.Level).Subtract(count);

            Touch(utcNow);
        }

        /// <summary>Повертає юнітів у гарнізон (після походу).</summary>
        public void ReceiveUnits(IReadOnlyDictionary<UnitStackKey, int> units, DateTime utcNow)
        {
            foreach (var (stack, count) in units)
            {
                if (count < 1)
                    continue;

                var unit = _units.FirstOrDefault(u => u.UnitType == stack.UnitType && u.Level == stack.Level);
                if (unit is null)
                {
                    unit = new VillageUnit(Guid.NewGuid(), Id, stack.UnitType, stack.Level);
                    _units.Add(unit);
                }
                unit.Add(count);
            }
            Touch(utcNow);
        }

        #endregion

        #region Підкріплення

        /// <summary>
        /// Приймає підкріплення від союзника.
        /// </summary>
        /// <param name="capacity">
        /// Скільки чужих юнітів вміщає посольство. Окремо від armyCapacity:
        /// підкріплення не належать господарю, не входять у його силу
        /// й не звільняють місця під власну армію.
        /// </param>
        public void AddReinforcements(Guid ownerPlayerId, Guid ownerGarrisonId,
            IReadOnlyDictionary<UnitStackKey, int> units, int capacity, DateTime utcNow)
        {
            var incoming = units.Values.Where(c => c > 0).Sum();

            if (incoming == 0)
                throw new RequirementNotMetException("Cannot reinforce with an empty army.");

            if (ReinforcementCount + incoming > capacity)
                throw new RequirementNotMetException(
                    $"Embassy capacity exceeded: {ReinforcementCount} of {capacity} used, incoming {incoming}.");

            foreach (var (stack, count) in units)
            {
                if (count < 1)
                    continue;

                var reinforcement = _reinforcements.FirstOrDefault(r =>
                    r.OwnerPlayerId == ownerPlayerId && r.UnitType == stack.UnitType && r.Level == stack.Level);

                if (reinforcement is null)
                {
                    reinforcement = new ReinforcementUnit(Guid.NewGuid(), Id, ownerPlayerId, ownerGarrisonId,
                        stack.UnitType, stack.Level, 0, utcNow);
                    _reinforcements.Add(reinforcement);
                }

                reinforcement.Add(count);
            }

            RaiseDomainEvent(new Events.ReinforcementsMoved(ownerGarrisonId, utcNow));
            Touch(utcNow);
        }

        /// <summary>
        /// Знімає підкріплення одного союзника — відкликання, кік або вихід
        /// із клану. Повертає склад, який має вирушити додому.
        /// </summary>
        public Dictionary<UnitStackKey, int> WithdrawReinforcements(Guid ownerPlayerId, DateTime utcNow)
        {
            var stacks = _reinforcements
                .Where(r => r.OwnerPlayerId == ownerPlayerId)
                .ToList();

            var withdrawn = stacks
                .Where(r => r.Count > 0)
                .ToDictionary(r => new UnitStackKey(r.UnitType, r.Level), r => r.Count);

            if (withdrawn.Count == 0)
                return [];

            // Гарнізон власника читаємо до видалення — після нього стеків уже немає
            var ownerGarrisonId = stacks[0].OwnerGarrisonId;

            _reinforcements.RemoveAll(r => r.OwnerPlayerId == ownerPlayerId);

            RaiseDomainEvent(new Events.ReinforcementsMoved(ownerGarrisonId, utcNow));
            Touch(utcNow);

            return withdrawn;
        }

        #endregion

        #region Втрати, лікування, викуп

        /// <summary>
        /// Знімає з оборони полеглих. Свої юніти зникають зі стеків гарнізону,
        /// чужі — зі стеків підкріплення відповідного власника.
        ///
        /// Порожні стеки підкріплень лишаються: власник далі числиться тут,
        /// і повернення додому має що відправити, навіть якщо це нуль.
        /// </summary>
        public void ApplyDefenceLosses(IReadOnlyList<StackLoss> losses, DateTime utcNow)
        {
            // Власники, чиї стеки постраждали: у кожного змінилась армія,
            // і кожному треба перерахувати Power окремо
            var affected = new HashSet<Guid>();

            foreach (var loss in losses)
            {
                if (loss.Lost <= 0)
                    continue;

                if (loss.OwnerPlayerId is null)
                {
                    var own = _units.FirstOrDefault(u => u.UnitType == loss.UnitType && u.Level == loss.Level);

                    own?.Subtract(loss.Lost);

                    continue;
                }

                var stack = _reinforcements.FirstOrDefault(r =>
                    r.OwnerPlayerId == loss.OwnerPlayerId && r.UnitType == loss.UnitType && r.Level == loss.Level);

                if (stack is null)
                    continue;

                stack.Subtract(loss.Lost);
                affected.Add(stack.OwnerGarrisonId);
            }

            _units.RemoveAll(u => u.Count <= 0);

            foreach (var ownerGarrisonId in affected)
                RaiseDomainEvent(new Events.ReinforcementsMoved(ownerGarrisonId, utcNow));

            Touch(utcNow);
        }

        /// <summary>Приймає поранених після бою (у межах вільної місткості).</summary>
        public void AdmitWounded(IReadOnlyDictionary<UnitStackKey, int> wounded, DateTime utcNow)
        {
            foreach (var (stack, count) in wounded)
            {
                if (count <= 0)
                    continue;

                var wound = _wounded.FirstOrDefault(w => w.UnitType == stack.UnitType && w.Level == stack.Level);
                if (wound is null)
                {
                    wound = new WoundedUnit(Guid.NewGuid(), Id, stack.UnitType, stack.Level, 0);
                    _wounded.Add(wound);
                }
                wound.Add(count);
            }
            Touch(utcNow);
        }

        /// <summary>
        /// Виліковує поранених: вони повертаються в гарнізон.
        /// </summary>
        public Dictionary<UnitStackKey, int> HealWounded(IReadOnlyDictionary<UnitStackKey, int> toHeal, DateTime utcNow)
        {
            var healed = new Dictionary<UnitStackKey, int>();

            foreach (var (stack, requested) in toHeal)
            {
                var wound = _wounded.FirstOrDefault(w => w.UnitType == stack.UnitType && w.Level == stack.Level);
                if (wound is null || requested <= 0)
                    continue;

                var count = Math.Min(requested, wound.Count);
                wound.Reduce(count);
                healed[stack] = count;
            }

            _wounded.RemoveAll(w => w.Count <= 0);

            if (healed.Count > 0)
                ReceiveUnits(healed, utcNow);

            Touch(utcNow);
            return healed;
        }

        /// <summary>Записує відновлюваних після бою — окремим стеком зі своїм дедлайном.</summary>
        public void AddRecoverable(IReadOnlyDictionary<UnitStackKey, int> units, Guid battleReportId, DateTime expiresAt, DateTime utcNow)
        {
            foreach (var (stack, count) in units)
            {
                if (count <= 0)
                    continue;

                _recoverable.Add(new RecoverableUnit(Guid.NewGuid(), Id, battleReportId, stack.UnitType, stack.Level, count, expiresAt));
            }
            Touch(utcNow);
        }

        /// <summary>
        /// Викуповує юнітів: вони повертаються в гарнізон.
        /// Списує зі стеків у порядку найближчого дедлайну — щоб гравець не втратив те, що згорає першим.
        /// </summary>
        public Dictionary<UnitStackKey, int> RecoverUnits(IReadOnlyDictionary<UnitStackKey, int> toRecover, DateTime utcNow)
        {
            var recovered = new Dictionary<UnitStackKey, int>();

            foreach (var (stack, requested) in toRecover)
            {
                if (requested <= 0)
                    continue;

                var remaining = requested;
                var candidates = _recoverable
                    .Where(r => r.UnitType == stack.UnitType && r.Level == stack.Level && r.IsActive(utcNow))
                    .OrderBy(r => r.ExpiresAt);

                foreach (var candidate in candidates)
                {
                    if (remaining <= 0)
                        break;

                    var taken = Math.Min(remaining, candidate.Count);
                    candidate.Reduce(taken);
                    remaining -= taken;
                }

                var total = requested - remaining;
                if (total > 0)
                    recovered[stack] = total;
            }

            _recoverable.RemoveAll(r => r.Count <= 0);

            if (recovered.Count > 0)
                ReceiveUnits(recovered, utcNow);

            Touch(utcNow);
            return recovered;
        }

        #endregion

        #region Внутрішнє

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;

        #endregion
    }
}
