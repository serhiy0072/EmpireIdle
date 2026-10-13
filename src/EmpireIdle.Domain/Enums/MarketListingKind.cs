namespace EmpireIdle.Domain.Enums
{
    /// <summary>Що виставлено на ринок.</summary>
    public enum MarketListingKind
    {
        /// <summary>Екземпляр спорядження зі своїми статами й заточкою.</summary>
        Equipment = 1,

        // 2 — колишній Hero: торгівлю героями прибрано (GDD §6.1, рішення 07.10.2026), значення не перевикористовуємо

        /// <summary>Пачка стакового предмета.</summary>
        Item = 3
    }
}
