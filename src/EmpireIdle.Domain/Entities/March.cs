using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>Стан походу.</summary>
    public enum MarchState
    {
        /// <summary>Іде до цілі.</summary>
        Outbound = 1,

        /// <summary>Повертається додому.</summary>
        Returning = 2,

        /// <summary>Завершений (армія вдома).</summary>
        Completed = 3
    }

    /// <summary>Тип цілі походу.</summary>
    public enum MarchTargetType
    {
        Monster = 1,
        Village = 2
    }

    /// <summary>
    /// Похід армії до цілі. Юніти зняті з гарнізону на час маршу
    /// і зберігаються тут (склад армії).
    /// </summary>
    public class March : Entity
    {
        private readonly List<MarchUnit> _units = new();

        public int ServerId { get; private set; }
        public Guid GarrisonId { get; private set; }

        /// <summary>Звідки вийшла армія (щоб знати, куди повертатись).</summary>
        public int OriginX { get; private set; }
        public int OriginY { get; private set; }

        public int TargetX { get; private set; }
        public int TargetY { get; private set; }

        public MarchTargetType TargetType { get; private set; }
        public Guid TargetId { get; private set; }

        public MarchState State { get; private set; }

        /// <summary>Коли армія прибуде в поточну точку призначення.</summary>
        public DateTime ArrivesAt { get; private set; }

        /// <summary>Склад армії (тільки для читання).</summary>
        public IReadOnlyCollection<MarchUnit> Units => _units.AsReadOnly();

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        public March(Guid id, int serverId, Guid garrisonId,
            int originX, int originY, int targetX, int targetY,
            MarchTargetType targetType, Guid targetId,
            IReadOnlyDictionary<string, int> units, DateTime arrivesAt) : base(id)
        {
            ServerId = serverId;
            GarrisonId = garrisonId;
            OriginX = originX;
            OriginY = originY;
            TargetX = targetX;
            TargetY = targetY;
            TargetType = targetType;
            TargetId = targetId;
            State = MarchState.Outbound;
            ArrivesAt = arrivesAt;

            foreach (var (unitType, count) in units)
                _units.Add(new MarchUnit(Guid.NewGuid(), id, unitType, count));
        }

        protected March() { } // Для EF Core

        /// <summary>Склад армії у вигляді словника (для повернення в гарнізон).</summary>
        public Dictionary<string, int> GetUnits()  => _units.ToDictionary(u => u.UnitType, u => u.Count);

        /// <summary>
        /// Армія дійшла до цілі й розвертається додому.
        /// Бій відбувається окремо (фаза Combat) — тут лише рух.
        /// </summary>
        public void TurnBack(TimeSpan returnDuration, DateTime utcNow)
        {
            if (State != MarchState.Outbound)
                throw new InvalidStateException($"March {Id} is not outbound.");

            State = MarchState.Returning;
            ArrivesAt = utcNow + returnDuration;

            Touch();
        }

        /// <summary>Армія повернулася додому — похід завершено.</summary>
        public void Complete(DateTime utcNow)
        {
            if (State != MarchState.Returning)
                throw new InvalidStateException($"March {Id} is not returning.");

            State = MarchState.Completed;
            RaiseDomainEvent(new Events.MarchReturned(Id, GarrisonId, utcNow));

            Touch();
        }

        /// <summary>
        /// Застосовує втрати після бою: зменшує склад армії.
        /// Загони, що загинули повністю, видаляються.
        /// </summary>
        public void ApplyLosses(IReadOnlyDictionary<string, int> losses)
        {
            foreach (var (unitType, lost) in losses)
            {
                var stack = _units.FirstOrDefault(u => u.UnitType == unitType);
                if (stack is null || lost <= 0)
                    continue;
                stack.Reduce(lost);
            }
            _units.RemoveAll(u => u.Count <= 0);

            Touch();
        }

        /// <summary>Фіксує факт бою для сповіщення гравця.</summary>
        public void RecordBattle(Guid playerId, Guid reportId, bool won, string targetName, DateTime utcNow)
        {
            RaiseDomainEvent(new Events.BattleFought(GarrisonId, playerId, Id, reportId, won, targetName, utcNow));
            Touch();
        }

        /// <summary>Прискорює прибуття (speedup за gems).</summary>
        public void ReduceTravelTime(TimeSpan reduction)
        {
            ArrivesAt -= reduction;
            Touch();
        }

        private void Touch() => UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Загін у складі походу.</summary>
    public class MarchUnit : Entity
    {
        public Guid MarchId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }

        public MarchUnit(Guid id, Guid marchId, string unitType, int count) : base(id)
        {
            MarchId = marchId;
            UnitType = unitType;
            Count = count;
        }

        protected MarchUnit() { } // Для EF Core

        /// <summary>Зменшує кількість юнітів у загоні (втрати в бою).</summary>
        public void Reduce(int amount)
        {
            Count = Math.Max(0, Count - amount);
        }
    }
}
