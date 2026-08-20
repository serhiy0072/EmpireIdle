namespace EmpireIdle.Domain.Entities
{
    /// <summary>Лічильник однієї цілі квесту.</summary>
    public class QuestObjectiveProgress
    {
        public Guid QuestProgressId { get; private set; }
        public int Index { get; private set; }
        public int Amount { get; private set; }

        /// <summary>Зафіксовано зі старту — зміна конфіга не рухає ціль.</summary>
        public int Required { get; private set; }

        public bool IsMet => Amount >= Required;

        public QuestObjectiveProgress(Guid questProgressId, int index, int required)
        {
            QuestProgressId = questProgressId;
            Index = index;
            Required = required;
        }

        public QuestObjectiveProgress() { } // для EF Core

        internal void Add(int amount) => Amount += amount;

        /// <summary>Підіймає лічильник до значення, якщо воно більше за поточне.</summary>
        internal void RaiseTo(int current)
        {
            if (current > Amount)
                Amount = current;
        }

        /// <summary>Обнуляє лічильник і оновлює потрібну кількість із конфіга.</summary>
        internal void ResetTo(int required)
        {
            Amount = 0;
            Required = required;
        }
    }
}
