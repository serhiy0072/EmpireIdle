using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Кланові технології (рішення 04.11): підношення лише наповнюють запас очок, а офіцер потім
    /// розподіляє його по вузлах. Так внесок і нагорода ніколи не залежать від того, чи зайшов офіцер.
    /// Окремий агрегат, а не частина клану: підношення йдуть часто й паралельно, а токен кореня клану
    /// серіалізував би разом із ними вступи, ролі й споруди.
    /// </summary>
    public class ClanResearch : Entity
    {
        public Guid ClanId { get; private set; }

        public int ServerId { get; private set; }

        /// <summary>Вивчені рівні: «тір:вузол» → рівень.</summary>
        public IReadOnlyDictionary<string, int> Levels { get; private set; } = new Dictionary<string, int>();

        /// <summary>Вкладені очки до наступного рівня: «тір:вузол» → очки.</summary>
        public IReadOnlyDictionary<string, int> Progress { get; private set; } = new Dictionary<string, int>();

        /// <summary>Запас очок, які офіцер ще не розподілив.</summary>
        public long UnallocatedPoints { get; private set; }

        /// <summary>Скільки підношень ресурсом зробив клан; за цим лічильником зсувається черга ресурсів.</summary>
        public long ResourceOfferings { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        public uint Version { get; private set; }

        public ClanResearch(Guid id, Guid clanId, int serverId, DateTime utcNow) : base(id)
        {
            ClanId = clanId;
            ServerId = serverId;
            UpdatedAt = utcNow;
        }

        protected ClanResearch() { } // для EF Core

        public int LevelOf(TechnologyKey key) => Levels.GetValueOrDefault(key.ToString());

        public int ProgressOf(TechnologyKey key) => Progress.GetValueOrDefault(key.ToString());

        /// <summary>Підношення за gems ресурс не витрачає, тому чергу зсуває лише підношення ресурсом.</summary>
        public void CountResourceOffering() => ResourceOfferings++;

        /// <summary>Очки підношення йдуть у запас клану.</summary>
        public void AddPoints(int points, DateTime utcNow)
        {
            UnallocatedPoints += Math.Max(points, 0);
            UpdatedAt = utcNow;
        }

        /// <summary>
        /// Вкладає очки із запасу у вузол і повертає закриті рівні. Чи відкритий вузол, вирішує дерево — це
        /// робить той, хто кличе. Вузол бере лише те, що йому ще потрібно: надлишок лишається в запасі.
        /// </summary>
        public IReadOnlyList<(TechnologyKey Key, int Level)> Allocate(TechnologyKey key, int points, ClanTechnologies technologies, DateTime utcNow)
        {
            if (points > UnallocatedPoints)
                throw new RequirementNotMetException(RefusalReasons.ClanTechNotEnoughPoints,
                    $"Clan {ClanId} has {UnallocatedPoints} unallocated points, {points} requested.", points, UnallocatedPoints);

            var node = technologies.Tree.Node(key.Node)
                ?? throw new EntityNotFoundException("Technology", key.Node);

            var name = key.ToString();
            var levels = new Dictionary<string, int>(Levels);
            var progress = new Dictionary<string, int>(Progress);

            var level = levels.GetValueOrDefault(name);
            var invested = progress.GetValueOrDefault(name);
            var left = points;
            var completed = new List<(TechnologyKey, int)>();

            while (left > 0 && level < node.Levels)
            {
                var needed = technologies.Points(key.Tier, node, level + 1) - invested;

                if (left < needed)
                {
                    invested += left;
                    left = 0;
                    break;
                }

                left -= needed;
                invested = 0;
                level++;
                levels[name] = level;
                completed.Add((key, level));
            }

            if (invested > 0)
                progress[name] = invested;
            else
                progress.Remove(name);

            Levels = levels;
            Progress = progress;
            UnallocatedPoints -= points - left;
            UpdatedAt = utcNow;

            // Одна подія на розподіл, скільки б рівнів він не закрив: перерахунок сили й так іде на весь клан
            if (completed.Count > 0)
                RaiseDomainEvent(new ClanTechnologyLevelsCompleted(ClanId, name, level, utcNow));

            return completed;
        }
    }
}
