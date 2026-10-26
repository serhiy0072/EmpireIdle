namespace EmpireIdle.Application.Common
{
    /// <summary>
    /// Технічна стеля кількості юнітів в одному рядку запиту. Не баланс: реальна армія на порядки менша.
    /// Без неї два стеки по int.MaxValue переповнювали б суму (LINQ Sum — checked) і давали 500.
    /// </summary>
    public static class UnitCountLimit
    {
        public const int MaxPerStack = 10_000_000;
    }
}
