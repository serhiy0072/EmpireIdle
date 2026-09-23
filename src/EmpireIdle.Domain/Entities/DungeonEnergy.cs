using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Енергія данжів гравця.
    ///
    /// Зберігається значенням на момент останнього дотику, а не тикає джобою:
    /// шкала лінійна й відновлюється розрахунком від часу — так не потрібен
    /// ані фоновий процес, ані рядок на кожного гравця щохвилини.
    /// </summary>
    public class DungeonEnergy : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Значення на момент RefreshedAt — поточне рахується від часу.</summary>
        public int Amount { get; private set; }

        public DateTime RefreshedAt { get; private set; }

        public uint Version { get; private set; }

        public DungeonEnergy(Guid id, Guid playerId, int amount, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            Amount = amount;
            RefreshedAt = utcNow;
        }

        protected DungeonEnergy() { } // для EF Core

        /// <summary>
        /// Скільки енергії зараз: збережене плюс накопичене за минулий час, але не понад стелю.
        /// </summary>
        public int Current(int maxEnergy, double regenHours, DateTime utcNow)
        {
            if (Amount >= maxEnergy || utcNow <= RefreshedAt)
                return Math.Min(Amount, maxEnergy);

            var perHour = maxEnergy / regenHours;
            var gained = (int)Math.Floor((utcNow - RefreshedAt).TotalHours * perHour);

            return Math.Min(maxEnergy, Amount + Math.Max(0, gained));
        }

        /// <summary>
        /// Коли шкала наповниться вщерть; null — вона вже повна.
        /// Клієнту потрібен саме момент, а не залишок: таймер він порахує сам.
        /// </summary>
        public DateTime? FullAt(int maxEnergy, double regenHours, DateTime utcNow)
        {
            var current = Current(maxEnergy, regenHours, utcNow);

            if (current >= maxEnergy)
                return null;

            var perHour = maxEnergy / regenHours;

            return utcNow.AddHours((maxEnergy - current) / perHour);
        }

        /// <summary>
        /// Списує вартість забігу. Спершу підтягує накопичене: інакше пауза
        /// між забігами губилася б при кожному списанні.
        /// </summary>
        public void Spend(int cost, int maxEnergy, double regenHours, DateTime utcNow)
        {
            var current = Current(maxEnergy, regenHours, utcNow);

            if (current < cost)
                throw new NotEnoughResourcesException("dungeon-energy", cost, current);

            Accrue(maxEnergy, regenHours, utcNow);
            Amount -= cost;
        }

        /// <summary>Нараховує енергію понад накопичене — нагорода або покупка.</summary>
        public void Add(int amount, int maxEnergy, double regenHours, DateTime utcNow)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Energy amount must be positive.");

            Accrue(maxEnergy, regenHours, utcNow);
            Amount = Math.Min(maxEnergy, Amount + amount);
        }

        /// <summary>
        /// Фіксує накопичене в Amount. RefreshedAt зсувається рівно на час, що дав
        /// цілі одиниці: дробовий залишок лишається й доростає далі. Колишнє
        /// RefreshedAt = utcNow губило його при кожному списанні — до одиниці за забіг.
        /// Повна шкала не росте, тож із неї відлік починається з цього моменту.
        /// </summary>
        private void Accrue(int maxEnergy, double regenHours, DateTime utcNow)
        {
            var current = Current(maxEnergy, regenHours, utcNow);

            if (current >= maxEnergy)
            {
                if (utcNow > RefreshedAt)
                    RefreshedAt = utcNow;
            }
            else if (utcNow > RefreshedAt)
            {
                var gained = current - Amount;
                RefreshedAt = RefreshedAt.AddHours(gained / (maxEnergy / regenHours));
            }

            Amount = current;
        }
    }
}
