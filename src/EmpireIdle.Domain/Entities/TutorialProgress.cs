using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Прогрес навчання гравця: які підказки він уже бачив і чи відмовився
    /// від ведення взагалі.
    ///
    /// Множина ключів, а не один прапорець: контекстні підказки з'являються
    /// протягом усієї гри (нова будівля, перший поранений), і кожну треба
    /// показати рівно один раз на всіх пристроях. Самі кроки й їх порядок
    /// живуть у клієнті — сервер лише пам'ятає, що вже показано.
    /// </summary>
    public class TutorialProgress : Entity
    {
        /// <summary>Довший ключ — помилка клієнта, а не новий крок.</summary>
        public const int MaxStepKeyLength = 64;

        private readonly List<string> _seenSteps = new();

        public Guid PlayerId { get; private set; }

        /// <summary>Ключі побачених кроків (тільки для читання).</summary>
        public IReadOnlyList<string> SeenSteps => _seenSteps.AsReadOnly();

        /// <summary>Коли гравець пропустив ведений туторіал; null — ще веде.</summary>
        public DateTime? SkippedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin): дві вкладки можуть позначати кроки одночасно.</summary>
        public uint Version { get; private set; }

        public TutorialProgress(Guid id, Guid playerId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            UpdatedAt = utcNow;
        }

        protected TutorialProgress() { } // для EF Core

        /// <summary>Позначає крок побаченим. Повторний виклик нічого не змінює — клієнт може надіслати двічі.</summary>
        /// <returns>true, якщо крок додано вперше.</returns>
        public bool MarkSeen(string stepKey, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(stepKey) || stepKey.Length > MaxStepKeyLength)
                throw new InvalidStateException($"Tutorial step key must be 1..{MaxStepKeyLength} characters.");

            if (_seenSteps.Contains(stepKey))
                return false;

            _seenSteps.Add(stepKey);
            UpdatedAt = utcNow;

            return true;
        }

        /// <summary>Гравець відмовився від ведення. Побачені кроки лишаються: підказки за них не повторюються.</summary>
        public void Skip(DateTime utcNow)
        {
            if (SkippedAt is not null)
                return;

            SkippedAt = utcNow;
            UpdatedAt = utcNow;
        }
    }
}
