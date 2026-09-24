namespace EmpireIdle.Domain.ValueObjects
{
    /// <summary>
    /// Що належить гравцеві за цей вхід. DailyDay — день серії або null,
    /// якщо сьогодні вже заходив чи щоденних нагород не задано.
    /// </summary>
    public readonly record struct LoginRewardsDue(int? DailyDay, bool Weekly, bool Monthly)
    {
        public static readonly LoginRewardsDue Nothing = new(null, false, false);

        public bool Any => DailyDay is not null || Weekly || Monthly;
    }
}
