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

        public int ServerId { get; private set; }
        public Guid PlayerId { get; private set; }
        public string QuestKey { get; private set; } = null!;
        public QuestState State { get; private set; }

        public DateTime StartedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        public DateTime? ClaimedAt { get; private set; }

        /// <summary>
        /// Момент останньої мутації агрегату. Змінюється навіть тоді, коли
        /// правились лише дочірні рядки — інакше токен паралелізму на корені
        /// не спрацював би, бо EF не оновив би рядок кореня.
        /// </summary>
        public DateTime UpdatedAt { get; private set; }

        public IReadOnlyCollection<QuestObjectiveProgress> Objectives => _objectives.AsReadOnly();

        public QuestProgress(Guid id, Guid playerId, int serverId, string questKey,
            IEnumerable<int> requiredCounts, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
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
            Touch();
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
            Touch();
        }

        /// <summary>Забрати нагороду. Ідемпотентно: повторний виклик нічого не робить.</summary>
        public bool Claim(DateTime utcNow)
        {
            if (State != QuestState.Completed)
                return false;

            State = QuestState.Claimed;
            ClaimedAt = utcNow;

            Touch();
            return true;
        }

        /// <summary>
        /// Скидає прогрес для Window=Daily. Лічильники обнуляються на місці —
        /// видалення й вставка рядків із тим самим складеним ключем
        /// дали б конфлікт у межах одного SaveChanges.
        /// </summary>
        public void Reset(IEnumerable<int> requiredCounts, DateTime utcNow)
        {
            var required = requiredCounts.ToList();

            for (var i = 0; i < _objectives.Count; i++)
                _objectives[i].ResetTo(i < required.Count ? required[i] : _objectives[i].Required);

            State = QuestState.InProgress;
            StartedAt = utcNow;
            CompletedAt = null;
            ClaimedAt = null;
            Touch();
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

            RaiseDomainEvent(new QuestCompleted(PlayerId, QuestKey, utcNow));
        }

        private void Touch() => UpdatedAt = DateTime.UtcNow;
    }
}
