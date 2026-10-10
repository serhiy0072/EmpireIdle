using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Що дав один ранг заточки.</summary>
    /// <param name="Position">Позиція бонусу на предметі, 0–3.</param>
    /// <param name="Stat">Ключ стату бонусу.</param>
    /// <param name="Step">Ступінь 1–4, що випав.</param>
    /// <param name="Value">Значення ступеня у відсотках — додається до бонусу.</param>
    public record ArtifactRank(int Position, string Stat, int Step, double Value);

    /// <summary>
    /// Розігрує ранги заточки артефактів (GDD §9.12).
    ///
    /// Ранги йдуть по колу: +1 → бонус 1, +2 → бонус 2, +3 → бонус 3, +4 → бонус 4, +5 — знову
    /// бонус 1. Перші два — сталі для типу слота, третій і четвертий випадають із пулу слота
    /// без повтору. Кожен ранг кидає ступінь 1–4 за BonusStepChances.
    ///
    /// Увесь випадок бере з сіда через DeterministicRandom, а не з IRandomSource напряму:
    /// сід лягає в журнал предмета, і будь-який ранг можна переграти. Скарга «точив тричі —
    /// і все одиниці» інакше не має відповіді.
    /// </summary>
    public class ArtifactRoller
    {
        private readonly EquipmentConfig _config;

        public ArtifactRoller(EquipmentConfig config)
        {
            _config = config;
        }

        /// <summary>Ранг, який дає заточка з <paramref name="currentMastery"/> на наступну.</summary>
        /// <param name="slotKey">Тип слота предмета: задає сталі бонуси й пул випадкових.</param>
        /// <param name="current">Бонуси, які вже є на предметі.</param>
        /// <exception cref="InvalidOperationException">Тип слота чи стат поза конфігом — битий конфіг.</exception>
        public ArtifactRank RollRank(string? slotKey, int currentMastery, IReadOnlyCollection<EquipmentStat> current, int seed)
        {
            var random = new DeterministicRandom(seed);

            var slot = _config.FindArtifactSlot(slotKey)
                ?? throw new InvalidOperationException($"Artifact slot '{slotKey}' is not configured.");

            var position = currentMastery % 4;

            // Стат позиції обирається лише раз — на першому її ранзі; далі той самий росте
            var stat = current.FirstOrDefault(s => s.Position == position)?.StatKey
                ?? (position < slot.FixedBonuses.Count
                    ? slot.FixedBonuses[position]
                    : PickRandom(slot, current, random));

            var bonus = _config.FindArtifactBonus(stat)
                ?? throw new InvalidOperationException($"Artifact bonus '{stat}' is not configured.");

            var step = PickStep(random);

            return new ArtifactRank(position, stat, step + 1, bonus.Steps[step]);
        }

        /// <summary>Випадковий бонус із пулу слота за вагами; той, що вже є, не повторюється.</summary>
        private static string PickRandom(ArtifactSlotConfig slot, IReadOnlyCollection<EquipmentStat> current, DeterministicRandom random)
        {
            var pool = slot.RandomBonuses
                .Where(entry => current.All(s => s.StatKey != entry.Stat))
                .ToList();

            if (pool.Count == 0)
                throw new InvalidOperationException($"Artifact slot '{slot.Key}' has no random bonus left to roll.");

            var roll = random.NextDouble() * pool.Sum(entry => entry.Weight);

            foreach (var entry in pool)
            {
                roll -= entry.Weight;

                if (roll < 0)
                    return entry.Stat;
            }

            // Похибка double на останньому кроці — беремо останній
            return pool[^1].Stat;
        }

        /// <summary>Індекс ступеня 0–3 за шансами.</summary>
        private int PickStep(DeterministicRandom random)
        {
            var roll = random.NextDouble();

            for (var i = 0; i < _config.BonusStepChances.Count; i++)
            {
                roll -= _config.BonusStepChances[i];

                if (roll < 0)
                    return i;
            }

            return _config.BonusStepChances.Count - 1;
        }
    }
}
