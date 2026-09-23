using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Dungeons
{
    /// <summary>
    /// Що сталося за один хід. Клієнт відтворює це анімацією — і саме тому
    /// журнал описує події, а не кінцевий стан: «вдарив на 120, ціль померла»
    /// малюється, а «у цілі тепер 0 здоров'я» — ні.
    /// </summary>
    public record TurnLog
    {
        public required int TurnNumber { get; init; }

        public required int Wave { get; init; }

        public required int Round { get; init; }

        /// <summary>Хто ходив.</summary>
        public required int ActorIndex { get; init; }

        /// <summary>Яке вміння застосоване; null — звичайний удар.</summary>
        public string? AbilityKey { get; init; }

        /// <summary>Хід пропущено через оглушення.</summary>
        public bool Stunned { get; init; }

        public required List<TurnEffect> Effects { get; init; }
    }

    /// <summary>Один наслідок ходу для однієї цілі.</summary>
    public record TurnEffect
    {
        public required int TargetIndex { get; init; }

        /// <summary>Завдана шкода після щита й зменшень.</summary>
        public double Damage { get; init; }

        public bool Critical { get; init; }

        public double Healed { get; init; }

        public double ShieldGained { get; init; }

        /// <summary>Шкода, яку поглинув щит цілі.</summary>
        public double ShieldAbsorbed { get; init; }

        public BattleStatusKind? StatusApplied { get; init; }

        public bool Died { get; init; }
    }
}
