using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Забіг у данж: стан бою між ходами гравця.
    ///
    /// Стан живе в агрегаті, а не в пам'яті процесу, бо ручне керування
    /// означає паузу будь-якої довжини між ходами — і перезавантажену
    /// сторінку, і закритий ноутбук. Енергія списується на старті:
    /// покинутий забіг не має бути безкоштовним.
    /// </summary>
    public class DungeonRun : Entity
    {
        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        public string DungeonKey { get; private set; } = null!;

        /// <summary>Рівень складності 1..MaxLevel: задає силу ворогів, кількість хвиль і рідкість нагороди.</summary>
        public int Level { get; private set; }

        public DungeonRunState State { get; private set; }

        /// <summary>Серіалізований BattleState — домен його не читає, лише зберігає.</summary>
        public string Battle { get; private set; } = null!;

        public DateTime StartedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        public DateTime? FinishedAt { get; private set; }

        /// <summary>Токен паралелізму: два ходи з двох вкладок не мають розійтись.</summary>
        public uint Version { get; private set; }

        public DungeonRun(Guid id, Guid playerId, int serverId, string dungeonKey, int level, string battle, DateTime utcNow)
            : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            DungeonKey = dungeonKey;
            Level = level;
            Battle = battle;
            State = DungeonRunState.InProgress;
            StartedAt = utcNow;
            UpdatedAt = utcNow;
        }

        protected DungeonRun() { } // для EF Core

        /// <summary>Записує стан після ходу. Завершений забіг ходів не приймає.</summary>
        public void Advance(string battle, DateTime utcNow)
        {
            RequireInProgress();

            Battle = battle;
            UpdatedAt = utcNow;
        }

        public void Win(string battle, DateTime utcNow) => Finish(DungeonRunState.Won, battle, utcNow);

        public void Lose(string battle, DateTime utcNow) => Finish(DungeonRunState.Lost, battle, utcNow);

        /// <summary>Гравець вийшов сам: енергія вже витрачена, нагороди немає.</summary>
        public void Abandon(DateTime utcNow) => Finish(DungeonRunState.Abandoned, Battle, utcNow);

        private void Finish(DungeonRunState state, string battle, DateTime utcNow)
        {
            RequireInProgress();

            State = state;
            Battle = battle;
            FinishedAt = utcNow;
            UpdatedAt = utcNow;
        }

        private void RequireInProgress()
        {
            if (State != DungeonRunState.InProgress)
                throw new InvalidStateException($"Dungeon run {Id} is already {State}.");
        }
    }
}
