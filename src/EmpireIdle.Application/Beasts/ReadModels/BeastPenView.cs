namespace EmpireIdle.Application.Beasts.ReadModels
{
    /// <summary>Звіринець гравця (GDD §5.10).</summary>
    /// <param name="Capacity">Скільки видів звірів вміщає; 0 — звіринець ще не відкритий.</param>
    /// <param name="Beasts">Приручені звірі.</param>
    /// <param name="Taming">Шанс і гарантія приручення для кожного типу звіра.</param>
    public record BeastPenView(int Capacity, List<BeastView> Beasts, List<BeastTamingView> Taming);

    public record BeastView(string BeastKey, int Rank, DateTime TamedAt);

    /// <param name="Chance">Шанс на перемогу з наміром «Приручити» при нинішньому рівні звіринця.</param>
    /// <param name="Misses">Перемог поспіль без звіра; на PityWins наступна перемога гарантована.</param>
    public record BeastTamingView(string BeastKey, string MonsterKey, double Chance, int Misses, int PityWins);
}
