using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Кланові технології (рішення 12.11): гравець сам вкладає підношення у вибрану технологію, запасу очок
    /// немає. Окремий агрегат, а не частина клану: підношення йдуть часто й паралельно, а токен кореня клану
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

        /// <summary>
        /// Вкладає очки підношення у вузол і повертає закриті рівні. Чи відкритий вузол, вирішує дерево — це
        /// робить той, хто кличе. Очки понад останній рівень пропадають: це щонайбільше залишок одного підношення.
        /// </summary>
        public IReadOnlyList<(TechnologyKey Key, int Level)> Invest(TechnologyKey key, int points, ClanTechnologies technologies, DateTime utcNow)
        {
            var node = technologies.Tree.Node(key.Node)
                ?? throw new EntityNotFoundException("Technology", key.Node);

            var name = key.ToString();
            var levels = new Dictionary<string, int>(Levels);
            var progress = new Dictionary<string, int>(Progress);

            var level = levels.GetValueOrDefault(name);
            var invested = progress.GetValueOrDefault(name);
            var left = Math.Max(points, 0);
            var completed = new List<(TechnologyKey, int)>();

            while (left > 0 && level < node.Levels)
            {
                var needed = technologies.Points(key.Tier, node, level + 1) - invested;

                if (left < needed)
                {
                    invested += left;
                    break;
                }

                left -= needed;
                invested = 0;
                level++;
                levels[name] = level;
                completed.Add((key, level));
            }

            if (invested > 0 && level < node.Levels)
                progress[name] = invested;
            else
                progress.Remove(name);

            Levels = levels;
            Progress = progress;
            UpdatedAt = utcNow;

            // Одна подія на підношення, скільки б рівнів воно не закрило: перерахунок сили й так іде на весь клан
            if (completed.Count > 0)
                RaiseDomainEvent(new ClanTechnologyLevelsCompleted(ClanId, name, level, utcNow));

            return completed;
        }
    }
}
