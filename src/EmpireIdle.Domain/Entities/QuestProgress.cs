using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Проходження квесту одним гравцем. Aggregate Root.
    /// Потрібні кількості копіюються з конфіга при старті — щоб зміна балансу
    /// не зсувала ціль гравцю, який уже в процесі.
    /// </summary>
    public class QuestProgress : Entity
    {
        private readonly List<QuestObjectiveProgress> _objectives = new();

        public Guid PlayerId { get; private set; }
        public string QuestKey { get; private set; } = null!;
        public QuestState State { get; private set; }

        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public DateTime? ClaimedAt { get; private set; }

        public IReadOnlyCollection<QuestObjectiveProgress> Objectives => _objectives.AsReadOnly();

        public QuestProgress(Guid id, Guid playerId, string questKey, IEnumerable<int> requiredCounts, DateTime utcNow)
            : base(id)
        {
            PlayerId = playerId;
            QuestKey = questKey;
            State = QuestState.InProgress;
            StartedAt = utcNow;

            var index = 0;
            foreach (var required in requiredCounts)
                _objectives.Add(new QuestObjectiveProgress(id, index++, required));
        }

        protected QuestProgress() { } // для EF Core

        /// <summary>Збільшує лічильник цілі (режим Accumulate).</summary>
        public void Advance(int objectiveIndex, int amount, DateTime utcNow)
        {
            if (State != QuestState.InProgress)
                return;

            Objective(objectiveIndex).Add(amount);
            TryComplete(utcNow);
        }

        /// <summary>
        /// Встановлює лічильник із поточного стану (режим Threshold).
        /// Назад не йде: втрата будівлі не скасовує вже досягнуту віху.
        /// </summary>
        public void SetProgress(int objectiveIndex, int current, DateTime utcNow)
        {
            if (State != QuestState.InProgress)
                return;

            Objective(objectiveIndex).RaiseTo(current);
            TryComplete(utcNow);
        }

        /// <summary>Забрати нагороду. Ідемпотентно: повторний виклик нічого не робить.</summary>
        public bool Claim(DateTime utcNow)
        {
            if (State != QuestState.Completed)
                return false;

            State = QuestState.Claimed;
            ClaimedAt = utcNow;

            return true;
        }

        /// <summary>Скидає прогрес для Window=Daily.</summary>
        public void Reset(IEnumerable<int> requiredCounts, DateTime utcNow)
        {
            _objectives.Clear();

            var index = 0;
            foreach (var required in requiredCounts)
                _objectives.Add(new QuestObjectiveProgress(Id, index++, required));

            State = QuestState.InProgress;
            StartedAt = utcNow;
            CompletedAt = null;
            ClaimedAt = null;
        }

        private QuestObjectiveProgress Objective(int index)
            => _objectives.FirstOrDefault(o => o.Index == index)
                ?? throw new InvalidOperationException($"Quest '{QuestKey}' has no objective at index {index}.");

        private void TryComplete(DateTime utcNow)
        {
            if (_objectives.Count == 0 || !_objectives.All(o => o.IsMet))
                return;

            State = QuestState.Completed;
            CompletedAt = utcNow;

            RaiseDomainEvent(new QuestCompleted(PlayerId, QuestKey));
        }
    }
}
