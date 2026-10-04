namespace EmpireIdle.Application.Beasts.ReadModels
{
    /// <summary>Звіринець гравця (GDD §5.10).</summary>
    /// <param name="Capacity">Скільки видів звірів вміщає; 0 — звіринець ще не відкритий.</param>
    /// <param name="Beasts">Приручені звірі.</param>
    /// <param name="Taming">Шанс і гарантія приручення для кожного типу звіра.</param>
    public record BeastPenView(int Capacity, List<BeastView> Beasts, List<BeastTamingView> Taming);

    /// <param name="Experience">Досвід усередині поточного рівня.</param>
    /// <param name="ExperienceToNext">Скільки корму треба на наступний рівень.</param>
    /// <param name="MaxLevel">Стеля рівня для нинішнього рангу.</param>
    public record BeastView(string BeastKey, int Rank, int Level, int Experience, int ExperienceToNext, int MaxLevel,
        DateTime TamedAt);

    /// <summary>Підсумок годування: Eaten — скільки корму з'їдено (решта лишилась в інвентарі).</summary>
    public record BeastFedView(string BeastKey, int Level, int Experience, int Eaten);

    /// <param name="Chance">Шанс на перемогу з наміром «Приручити» при нинішньому рівні звіринця.</param>
    /// <param name="Misses">Перемог поспіль без звіра; на PityWins наступна перемога гарантована.</param>
    public record BeastTamingView(string BeastKey, string MonsterKey, double Chance, int Misses, int PityWins);
}
