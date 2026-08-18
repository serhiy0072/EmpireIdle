using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Спільний прогрес серверного квесту. Total перераховується джобом із внесків —
    /// прямий інкремент зробив би цей рядок точкою конкуренції для всього серверу.
    /// </summary>
    public class ServerQuestProgress : Entity
    {
        public int ServerId { get; private set; }
        public string QuestKey { get; private set; } = null!;

        public long Total { get; private set; }
        public long Target { get; private set; }

        public QuestState State { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public ServerQuestProgress(Guid id, int serverId, string questKey, long target) : base(id)
        {
            ServerId = serverId;
            QuestKey = questKey;
            Target = target;
            State = QuestState.InProgress;
        }

        protected ServerQuestProgress() { } // для EF Core

        /// <summary>Оновлює суму з внесків. Повертає true, якщо квест щойно завершився.</summary>
        public bool UpdateTotal(long total, DateTime utcNow)
        {
            Total = total;

            if (State != QuestState.InProgress || Total < Target)
                return false;

            State = QuestState.Completed;
            CompletedAt = utcNow;

            return true;
        }
    }
}
