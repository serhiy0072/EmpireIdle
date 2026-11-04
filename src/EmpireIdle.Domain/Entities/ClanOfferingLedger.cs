using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Особиста книга підношень: кланові монети, спроби ресурсами, gems-підношення й очки дня.
    /// Живе за гравцем, а не за членством: монети лишаються при виході (рішення 03.11), а якби спроби
    /// чи лічильники трималися за клан, вихід і повернення обнуляли б кожен ліміт.
    /// </summary>
    public class ClanOfferingLedger : Entity
    {
        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        public long Coins { get; private set; }

        /// <summary>
        /// Скільки спроб витрачено й ще не відновлено. Лічимо витрачені, а не наявні: стеля залежить
        /// від технології клану, і так зміна стелі нічого не губить і не дарує.
        /// </summary>
        public int AttemptsSpent { get; private set; }

        /// <summary>Від якого моменту йде відновлення наступної спроби.</summary>
        public DateTime AttemptsAnchor { get; private set; }

        /// <summary>UTC-доба, до якої належать денні лічильники.</summary>
        public DateOnly Day { get; private set; }

        public int GemOfferingsToday { get; private set; }

        /// <summary>Очки всіх підношень за добу, у кожному клані разом: від них пороги денних нагород.</summary>
        public int PointsToday { get; private set; }

        public uint Version { get; private set; }

        public ClanOfferingLedger(Guid id, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            AttemptsAnchor = utcNow;
            Day = DateOnly.FromDateTime(utcNow);
        }

        protected ClanOfferingLedger() { } // для EF Core

        /// <summary>Скільки спроб доступно зараз за такої стелі.</summary>
        public int AttemptsAvailable(int maxAttempts, TimeSpan recharge, DateTime utcNow)
            => Math.Max(maxAttempts - SpentAt(recharge, utcNow), 0);

        /// <summary>Витрачає спробу; порожня шкала — чесна відмова, гравець бачить її за двома вкладками.</summary>
        public void SpendAttempt(int maxAttempts, TimeSpan recharge, DateTime utcNow)
        {
            var spent = SpentAt(recharge, utcNow);

            if (spent >= maxAttempts)
                throw new RequirementNotMetException(RefusalReasons.ClanTechNoAttempts, $"Player {PlayerId} has no offering attempts left.");

            // Частина інтервалу, що вже минула, лишається зарахованою; повна шкала рахує відновлення від цієї витрати
            var recovered = AttemptsSpent - spent;
            AttemptsAnchor = spent == 0 ? utcNow : AttemptsAnchor + recharge * recovered;
            AttemptsSpent = spent + 1;
        }

        /// <summary>Номер наступного gems-підношення за цю добу, від 1.</summary>
        public int NextGemOffering(DateTime utcNow) => (IsToday(utcNow) ? GemOfferingsToday : 0) + 1;

        public void RecordGemOffering(DateTime utcNow)
        {
            RollDay(utcNow);
            GemOfferingsToday++;
        }

        /// <summary>Очки підношення до денної суми; повертає суму до і після — за ними видно перетнуті пороги.</summary>
        public (int Before, int After) AddPoints(int points, DateTime utcNow)
        {
            RollDay(utcNow);

            var before = PointsToday;
            PointsToday += Math.Max(points, 0);

            return (before, PointsToday);
        }

        public void AddCoins(int coins) => Coins += Math.Max(coins, 0);

        private int SpentAt(TimeSpan recharge, DateTime utcNow)
        {
            if (AttemptsSpent == 0 || utcNow <= AttemptsAnchor)
                return AttemptsSpent;

            var recovered = (int)Math.Min((utcNow - AttemptsAnchor) / recharge, AttemptsSpent);

            return AttemptsSpent - recovered;
        }

        private bool IsToday(DateTime utcNow) => Day == DateOnly.FromDateTime(utcNow);

        private void RollDay(DateTime utcNow)
        {
            if (IsToday(utcNow))
                return;

            Day = DateOnly.FromDateTime(utcNow);
            GemOfferingsToday = 0;
            PointsToday = 0;
        }
    }
}
