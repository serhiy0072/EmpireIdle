namespace EmpireIdle.Domain.Enums
{
    /// <summary>Чим закінчилась перемога з наміром «Приручити».</summary>
    public enum TameOutcome
    {
        /// <summary>Кидок не вдався: гравець отримує звичайну здобич, лічильник гарантії росте.</summary>
        Missed = 1,

        /// <summary>Новий вид у звіринці.</summary>
        Tamed = 2,

        /// <summary>Дублікат підняв ранг наявного звіра.</summary>
        RankedUp = 3,

        /// <summary>Дублікат звіра з максимальним рангом — звичайна здобич.</summary>
        RankCapped = 4,

        /// <summary>Звіринець заповнився, поки армія йшла: звичайна здобич, гарантія не згоряє.</summary>
        NoRoom = 5
    }
}
