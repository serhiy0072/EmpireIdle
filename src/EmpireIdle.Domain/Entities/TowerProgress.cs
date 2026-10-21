using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Прогрес гравця у «Тисячній вежі» (рішення 18.10.2026): поверхи йдуть строго по черзі, кожен
    /// проходиться раз і закривається назавжди. Зберігається лише найвищий пройдений — решта виводиться з нього.
    /// Час досягнення розв'язує рівність у рейтингу: вище той, хто дійшов раніше.
    /// </summary>
    public class TowerProgress : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Сервер гравця — рейтинг вежі рахується в межах сервера.</summary>
        public int ServerId { get; private set; }

        /// <summary>Найвищий пройдений поверх; 0 — ще жодного.</summary>
        public int HighestFloor { get; private set; }

        /// <summary>Коли пройдено HighestFloor; null — ще нічого.</summary>
        public DateTime? ReachedAt { get; private set; }

        public uint Version { get; private set; }

        public TowerProgress(Guid id, Guid playerId, int serverId) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
        }

        protected TowerProgress() { } // для EF Core

        /// <summary>Поверх, який чекає на штурм.</summary>
        public int NextFloor => HighestFloor + 1;

        /// <summary>
        /// Чи можна штурмувати цей поверх: лише наступний за пройденим і не вище вершини. Інший поверх
        /// гравець бачить, коли вежу відкрито у двох вкладках — це чесна відмова, а не помилка клієнта.
        /// </summary>
        public void EnsureCanStorm(int floor, int maxFloor)
        {
            if (HighestFloor >= maxFloor)
                throw new RequirementNotMetException(RefusalReasons.TowerConquered,
                    $"Player {PlayerId} has already cleared all {maxFloor} floors.");

            if (floor != NextFloor)
                throw new RequirementNotMetException(RefusalReasons.TowerFloorClosed,
                    $"Floor {floor} is not open: the next floor is {NextFloor}.", floor, NextFloor);
        }

        /// <summary>Закриває пройдений поверх — повторно на нього вже не зайти.</summary>
        public void Clear(int floor, int maxFloor, DateTime utcNow)
        {
            EnsureCanStorm(floor, maxFloor);

            HighestFloor = floor;
            ReachedAt = utcNow;
        }
    }
}
