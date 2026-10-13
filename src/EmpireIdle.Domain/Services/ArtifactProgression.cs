using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Рівень артефакта (GDD §9.12): крива досвіду й скільки дає згодоване спорядження.
    /// Рівень починається з 0; крок до рівня n — round10(база + коефіцієнт · (n − 1)^показник),
    /// тож перший рівень коштує один звичайний артефакт, а L80 — близько місяця прокачки.
    /// </summary>
    public class ArtifactProgression
    {
        private readonly EquipmentConfig _config;

        public ArtifactProgression(EquipmentConfig config)
        {
            _config = config;
        }

        public int MaxLevel => _config.MaxLevel;

        /// <summary>Досвід на крок до рівня <paramref name="level"/> (від попереднього).</summary>
        public long StepTo(int level)
            => level < 1
                ? 0
                : (long)(Math.Round((_config.LevelExperienceBase
                    + _config.LevelExperienceCoefficient * Math.Pow(level - 1, _config.LevelExperienceExponent)) / 10) * 10);

        /// <summary>Накопичений досвід, з яким артефакт стає рівня <paramref name="level"/>.</summary>
        public long ExperienceToReach(int level)
        {
            long total = 0;

            for (var n = 1; n <= Math.Min(level, _config.MaxLevel); n++)
                total += StepTo(n);

            return total;
        }

        /// <summary>Рівень для накопиченого досвіду, не вище за стелю.</summary>
        public int LevelFor(long experience)
        {
            var level = 0;

            while (level < _config.MaxLevel && experience >= ExperienceToReach(level + 1))
                level++;

            return level;
        }

        /// <summary>Скільки ще досвіду до наступного рівня; null — артефакт на стелі.</summary>
        public long? ExperienceToNext(EquipmentItem item)
            => item.Level >= _config.MaxLevel ? null : ExperienceToReach(item.Level + 1) - item.Experience;

        /// <summary>
        /// Досвід, який дає згодований предмет: базовий за рідкістю плюс увесь вкладений у нього.
        /// null — рідкість не годується.
        /// </summary>
        public long? FeedValue(EquipmentItem food)
            => _config.FeedExperience.TryGetValue(food.Rarity, out var basis) ? basis + food.Experience : null;
    }
}
