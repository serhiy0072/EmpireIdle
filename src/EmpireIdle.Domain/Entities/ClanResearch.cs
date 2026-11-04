using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Кланові технології (рішення 28.10.2026): вивчені рівні, накопичені очки й обрана ціль.
    /// Окремий агрегат, а не частина клану: підношення йдуть часто й паралельно, а токен кореня клану
    /// серіалізував би разом із ними вступи, ролі й споруди.
    /// </summary>
    public class ClanResearch : Entity
    {
        public Guid ClanId { get; private set; }

        public int ServerId { get; private set; }

        /// <summary>Вивчені рівні: «тір:вузол» → рівень.</summary>
        public IReadOnlyDictionary<string, int> Levels { get; private set; } = new Dictionary<string, int>();

        /// <summary>Очки до наступного рівня: «тір:вузол» → очки. Переживають зміну цілі.</summary>
        public IReadOnlyDictionary<string, int> Progress { get; private set; } = new Dictionary<string, int>();

        /// <summary>Що клан вивчає зараз («тір:вузол»); null — ціль не обрано.</summary>
        public string? TargetKey { get; private set; }

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

        /// <summary>Обирає ціль. Чи доступний вузол, вирішує дерево — це робить той, хто кличе.</summary>
        public void SelectTarget(TechnologyKey key, DateTime utcNow)
        {
            TargetKey = key.ToString();
            UpdatedAt = utcNow;
        }

        /// <summary>
        /// Додає очки підношення до цілі й повертає рівні, які вони закрили. Закритий рівень сам веде до
        /// наступного рівня того ж вузла; надлишок переходить у нього. Вивчений до кінця вузол знімає ціль,
        /// і надлишок згорає: куди далі, вирішує офіцер, а не сервер.
        /// </summary>
        public IReadOnlyList<(TechnologyKey Key, int Level)> AddPoints(int points, ClanTechnologies technologies, DateTime utcNow)
        {
            if (TargetKey is not { } target
                || TechnologyKey.Parse(target) is not { } key
                || technologies.Tree.Node(key.Node) is not { } node)
                throw new RequirementNotMetException(RefusalReasons.ClanTechNotSelected, $"Clan {ClanId} has no technology selected.");

            var levels = new Dictionary<string, int>(Levels);
            var progress = new Dictionary<string, int>(Progress);

            var level = levels.GetValueOrDefault(target);
            var accumulated = progress.GetValueOrDefault(target) + Math.Max(points, 0);
            var completed = new List<(TechnologyKey, int)>();

            while (level < node.Levels && accumulated >= technologies.Points(key.Tier, node, level + 1))
            {
                accumulated -= technologies.Points(key.Tier, node, level + 1);
                level++;
                levels[target] = level;
                completed.Add((key, level));
            }

            if (level >= node.Levels)
            {
                progress.Remove(target);
                TargetKey = null;
            }
            else
            {
                progress[target] = accumulated;
            }

            Levels = levels;
            Progress = progress;
            UpdatedAt = utcNow;

            return completed;
        }
    }
}
