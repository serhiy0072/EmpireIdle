namespace EmpireIdle.Domain.Entities
{
    /// <summary>На що діє активний ефект.</summary>
    public enum EffectTarget
    {
        /// <summary>Виробництво ресурсів.</summary>
        Production = 1,

        /// <summary>Сила атаки в бою.</summary>
        Attack = 2,

        /// <summary>Сила захисту в бою.</summary>
        Defense = 3
    }

    /// <summary>
    /// Тимчасовий ефект гравця: множник, що діє до вказаного часу.
    /// Створюється використанням буста, читається тіком і боєм.
    /// </summary>
    public class ActiveEffect : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>На що діє.</summary>
        public EffectTarget Target { get; private set; }

        /// <summary>Множник (2.0 = подвоєння, 1.25 = +25%).</summary>
        public double Multiplier { get; private set; }

        /// <summary>Коли ефект перестає діяти.</summary>
        public DateTime ExpiresAt { get; private set; }

        /// <summary>Ключ предмета-джерела (для відображення й аналітики).</summary>
        public string SourceItemKey { get; private set; } = null!;

        /// <summary>Чи цей ефект створено вказаним предметом.</summary>
        public bool IsFrom(string itemKey) => SourceItemKey == itemKey;

        public ActiveEffect(Guid id, Guid playerId, EffectTarget target, double multiplier,
            DateTime expiresAt, string sourceItemKey) : base(id)
        {
            if (multiplier <= 0)
                throw new InvalidOperationException("Multiplier must be positive.");

            PlayerId = playerId;
            Target = target;
            Multiplier = multiplier;
            ExpiresAt = expiresAt;
            SourceItemKey = sourceItemKey;
        }

        protected ActiveEffect() { } // Для EF Core

        /// <summary>Чи діє ефект на вказаний момент.</summary>
        public bool IsActive(DateTime utcNow) => ExpiresAt > utcNow;

        /// <summary>Продовжує дію (повторне використання того самого буста).</summary>
        public void Extend(TimeSpan duration) => ExpiresAt += duration;

        /// <summary>Перезапускає ефект із новим множником і часом.</summary>
        public void Restart(double multiplier, DateTime expiresAt, string sourceItemKey)
        {
            if (multiplier <= 0)
                throw new InvalidOperationException("Multiplier must be positive.");

            Multiplier = multiplier;
            ExpiresAt = expiresAt;
            SourceItemKey = sourceItemKey;
        }
    }
}