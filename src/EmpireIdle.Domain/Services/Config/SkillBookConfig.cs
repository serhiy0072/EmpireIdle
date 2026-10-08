using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Книга вмінь (GDD §6.1, рішення 08.10.2026): роль × рідкість × половина — 18 видів.
    /// Книга підходить лише героєві своєї ролі й рідкості й лише до вміння своєї половини.
    /// Сама книга — звичайний предмет інвентаря (Type = skillbook); тут лише, до кого вона йде.
    /// </summary>
    public class SkillBookConfig
    {
        /// <summary>Ключ предмета з Items.</summary>
        public string ItemKey { get; set; } = null!;

        /// <summary>Клас (роль) героя з HeroesConfig.Classes.</summary>
        public string Class { get; set; } = null!;

        public Rarity Rarity { get; set; }

        public SkillHalf Half { get; set; }
    }
}
