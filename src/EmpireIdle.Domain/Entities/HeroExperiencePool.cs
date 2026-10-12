using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Досвід героїв гравця (GDD §6.1, рішення 06.10.2026): копиться поза конкретним героєм —
    /// з баночок, данжів, нагород — і витрачається на рівень обраного героя.
    ///
    /// Окремий агрегат із власним токеном, а не поле гравця: два паралельні підняття рівня
    /// інакше витратили б той самий досвід двічі.
    /// </summary>
    public class HeroExperiencePool : Entity
    {
        public Guid PlayerId { get; private set; }
        public int ServerId { get; private set; }

        /// <summary>Скільки досвіду ще не витрачено.</summary>
        public long Amount { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public HeroExperiencePool(Guid id, Guid playerId, int serverId) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
        }

        protected HeroExperiencePool() { } // Для EF Core

        public void Add(long amount)
        {
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Hero experience to add must be positive.");

            Amount = checked(Amount + amount);
        }

        /// <summary>Знімає досвід; не вистачає — відмова з цифрами, пул не змінюється.</summary>
        public void Spend(long amount)
        {
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Hero experience to spend must be positive.");

            if (Amount < amount)
                throw new NotEnoughResourcesException("heroExperience", amount, Amount);

            Amount -= amount;
        }
    }
}
