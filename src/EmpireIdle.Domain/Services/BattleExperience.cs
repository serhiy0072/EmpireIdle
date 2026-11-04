namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Досвід героїв за бій під технологіями наставництва (рішення 28.10.2026): база × (1 + бонус),
    /// до цілого. Лише бойовий досвід — монстри й данжі; книги, предмети й нагороди квестів не підсилюються,
    /// інакше технологія множила б і те, що гравець уже купив.
    /// </summary>
    public static class BattleExperience
    {
        public static long Boosted(long experience, double bonus)
            => experience <= 0 || bonus <= 0
                ? experience
                : (long)Math.Round(experience * (1 + bonus), MidpointRounding.AwayFromZero);
    }
}
