using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Місце гравця в серверному топі.
    ///
    /// Дві різні природи даних в одній сутності, і це навмисно:
    ///
    /// Лічильники активності накопичуються подіями — вбиті монстри, виграні
    /// бої, забрані квести. Джерела для перерахунку немає: бій не лишає рядка,
    /// який можна перерахувати. Пропущена подія коштує невеликого недоліку
    /// назавжди — прийнятно, бо лічильники монотонні й не розганяють похибку.
    ///
    /// Компоненти рейтингу навпаки перераховуються цілком із поточного стану:
    /// сила й рівні будівель читаються заново, тож поразка одразу опускає топ.
    /// </summary>
    public class PlayerRating : Entity
    {
        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        // ---------- Накопичені лічильники ----------

        public int MonstersDefeated { get; private set; }

        public int BattlesWon { get; private set; }

        public int QuestsCompleted { get; private set; }

        public int ServerContribution { get; private set; }

        // ---------- Перераховані компоненти ----------

        public double PowerScore { get; private set; }

        public double DevelopmentScore { get; private set; }

        public double ActivityScore { get; private set; }

        /// <summary>Підсумок — за ним сортується топ.</summary>
        public int TotalRating { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public PlayerRating(Guid id, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            UpdatedAt = utcNow;
        }

        protected PlayerRating() { } // для EF Core

        /// <summary>Записує подію активності. Лічильники лише ростуть.</summary>
        public void RecordActivity(int monsters = 0, int battlesWon = 0, int quests = 0, int contribution = 0)
        {
            MonstersDefeated += monsters;
            BattlesWon += battlesWon;
            QuestsCompleted += quests;
            ServerContribution += contribution;
        }

        /// <summary>
        /// Перераховує рейтинг із поточного стану.
        /// </summary>
        /// <param name="power">Бойова сила з PlayerPower.</param>
        /// <param name="buildingLevelSum">Сума рівнів усіх будівель села.</param>
        public void Recalculate(double power, int buildingLevelSum, RatingConfig config, DateTime utcNow)
        {
            var activityPoints =
                MonstersDefeated * config.PointsPerMonster
                + BattlesWon * config.PointsPerBattleWon
                + QuestsCompleted * config.PointsPerQuest
                + ServerContribution * config.PointsPerContribution;

            PowerScore = Normalise(power, config.PowerReference) * config.PowerWeight;
            DevelopmentScore = Normalise(buildingLevelSum, config.DevelopmentReference) * config.DevelopmentWeight;
            ActivityScore = Normalise(activityPoints, config.ActivityReference) * config.ActivityWeight;

            TotalRating = (int)((PowerScore + DevelopmentScore + ActivityScore) * config.Scale);
            UpdatedAt = utcNow;
        }

        /// <summary>
        /// Частка від орієнтира, обрізана одиницею. Обрізання — і є стеля:
        /// вичерпавши бойову вісь, гравець мусить рости іншими.
        /// </summary>
        private static double Normalise(double value, double reference)
            => reference <= 0 ? 0 : Math.Clamp(value / reference, 0, 1);
    }
}
