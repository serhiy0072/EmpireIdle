namespace EmpireIdle.Domain.Enums
{
    /// <summary>Хто накопичує прогрес квесту.</summary>
    public enum QuestScope
    {
        Personal = 1,
        Server = 2,

        /// <summary>Спільний прогрес клану: внески учасників складаються, нагорода — клану (GDD §7.2).</summary>
        Clan = 3
    }
}
