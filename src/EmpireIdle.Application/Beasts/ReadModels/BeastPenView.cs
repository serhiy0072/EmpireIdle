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
    /// <param name="Passive">Пасивка на нинішньому рівні й стан її таймерів.</param>
    public record BeastView(string BeastKey, int Rank, int Level, int Experience, int ExperienceToNext, int MaxLevel,
        DateTime TamedAt, BeastPassiveView Passive);

    /// <param name="Effect">На що діє: Production, Attack, Defense, MarchSpeed, Carry.</param>
    /// <param name="Bonus">Надбавка на нинішньому рівні: 0.15 — +15%.</param>
    /// <param name="ActiveUntil">До коли діє; null або минуле — не діє.</param>
    /// <param name="CooldownUntil">Коли знову можна активувати; null або минуле — готова.</param>
    public record BeastPassiveView(string Effect, double Bonus, int DurationMinutes, int CooldownMinutes, int ActivationFood,
        DateTime? ActiveUntil, DateTime? CooldownUntil);

    public record BeastActivatedView(string BeastKey, DateTime ActiveUntil, DateTime CooldownUntil);

    /// <summary>Підсумок годування: Eaten — скільки корму з'їдено (решта лишилась в інвентарі).</summary>
    public record BeastFedView(string BeastKey, int Level, int Experience, int Eaten);

    /// <param name="Chance">Шанс на перемогу з наміром «Приручити» при нинішньому рівні звіринця.</param>
    /// <param name="Misses">Перемог поспіль без звіра; на PityWins наступна перемога гарантована.</param>
    public record BeastTamingView(string BeastKey, string MonsterKey, double Chance, int Misses, int PityWins);
}
