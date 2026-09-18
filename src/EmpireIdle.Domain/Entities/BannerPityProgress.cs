using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Прогрес pity гравця в групі банерів. Ключ — група, а не банер:
    /// новий банер того самого типу продовжує накопичене (§6.3).
    ///
    /// Без ServerId: ролл оплачується акаунтними gems, тож і гарантія акаунтна.
    /// </summary>
    public class BannerPityProgress : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Тип банера, наприклад "hero".</summary>
        public string PityGroup { get; private set; } = null!;

        /// <summary>Роллів поспіль без рідкісного.</summary>
        public int RareSince { get; private set; }

        /// <summary>Роллів поспіль без унікального.</summary>
        public int UniqueSince { get; private set; }

        /// <summary>Попередній унікальний програв 50/50, тож наступний буде банерним.</summary>
        public bool FeaturedGuaranteed { get; private set; }

        /// <summary>Скільки всього роллів у групі — для аналітики й підтримки.</summary>
        public int TotalRolls { get; private set; }

        /// <summary>Токен паралелізму: два одночасні ролли інакше з'їли б одну гарантію двічі.</summary>
        public uint Version { get; private set; }

        public BannerPityProgress(Guid id, Guid playerId, string pityGroup) : base(id)
        {
            PlayerId = playerId;
            PityGroup = pityGroup;
        }

        protected BannerPityProgress() { } // Для EF Core

        /// <summary>Поточний стан у вигляді, який приймає <see cref="BannerRoller"/>.</summary>
        public PityState State => new(RareSince, UniqueSince, FeaturedGuaranteed);

        /// <summary>Записує результат ролла. Самі правила переходу живуть у ролері.</summary>
        public void Apply(PityState state)
        {
            RareSince = state.RareSince;
            UniqueSince = state.UniqueSince;
            FeaturedGuaranteed = state.FeaturedGuaranteed;
            TotalRolls++;
        }
    }
}
