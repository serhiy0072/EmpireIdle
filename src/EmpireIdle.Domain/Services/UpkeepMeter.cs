namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Скільки цілих одиниць провізії набігло за проміжок. Лічильник іде від сталої точки
    /// (епохи Unix), а не від минулої оплати: floor(темп × кінець) − floor(темп × початок)
    /// телескопується, тож дріб не губиться між прогонами й не потребує окремого поля.
    /// Інакше округлення вниз на кожен 10-хвилинний прогін з'їдало б частку провізії,
    /// а темп нижче 6 за годину не списувався б ніколи.
    /// Коли темп змінюється між прогонами, похибка — не більше одиниці на зміну.
    /// </summary>
    public static class UpkeepMeter
    {
        public static int Due(double perHour, DateTime from, DateTime to)
        {
            if (perHour <= 0 || to <= from)
                return 0;

            // decimal, а не double: на мільйонах годин від епохи double дає 0.9999…, і floor губив би одиницю
            var rate = (decimal)perHour;
            var due = Units(rate, to) - Units(rate, from);

            return due >= int.MaxValue ? int.MaxValue : (int)due;
        }

        private static decimal Units(decimal perHour, DateTime at)
            => Math.Floor(perHour * (at - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerHour);
    }
}
