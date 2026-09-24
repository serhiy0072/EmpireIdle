namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Переклади назв з конфігів на одну мову. Базова мова — мова, якою
    /// написані самі конфіги (Localization.DefaultLanguage); локаль лише
    /// перекриває назви, а чого в ній немає, показується з конфіга.
    /// </summary>
    public class LocaleConfig
    {
        /// <summary>
        /// Назва за ключем «розділ.ключ»: building.townhall, hero.warrior_bran,
        /// item.teleport, resource.gold, unit.infantry, monster.wolf,
        /// artifactSet.dawn, artifactSlot.ring.
        /// </summary>
        public Dictionary<string, string> Names { get; set; } = new();
    }
}
