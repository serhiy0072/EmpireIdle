namespace EmpireIdle.Domain.Battles
{
    /// <summary>
    /// Бойові стати бійця одномоментного бою — героя чи конвою. Усе вже з урахуванням стель
    /// і частками, а не відсотками: рушій лише множить. Конвой має лише перші чотири,
    /// решта в нього нейтральні (Plain).
    /// </summary>
    /// <param name="AttackInterval">Секунд між ударами, вже зі швидкістю атаки.</param>
    /// <param name="CritMultiplier">У скільки разів крит сильніший за удар; 1 — криту немає.</param>
    /// <param name="BlockPower">Частка удару, яку гасить блок, — уже з базовою силою блоку.</param>
    /// <param name="Ward">Щит на старті бою часткою максимального здоров'я.</param>
    /// <param name="SkillDamage">Надбавка до шкоди вмінь: 0.2 — вміння б'ють на 20% сильніше.</param>
    /// <param name="CooldownMultiplier">Множник перезарядки вмінь: 0.8 — на 20% швидше.</param>
    public sealed record FighterStats(
        double Attack,
        double Defense,
        double MaxHealth,
        double AttackInterval,
        double CritChance = 0,
        double CritMultiplier = 1,
        double BlockChance = 0,
        double BlockPower = 0,
        double DamageReduction = 0,
        double Lifesteal = 0,
        double Ward = 0,
        double SkillDamage = 0,
        double CooldownMultiplier = 1)
    {
        /// <summary>Боєць без криту, блоку й умінь — конвой.</summary>
        public static FighterStats Plain(double attack, double defense, double maxHealth, double attackInterval)
            => new(attack, defense, maxHealth, attackInterval);
    }
}
