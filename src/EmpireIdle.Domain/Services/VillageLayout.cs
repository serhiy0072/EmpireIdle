namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Правила плану села (рішення 13.10.2026): гравець переставляє будівлі по сітці всередині муру,
    /// і будівлі не налазять одна на одну. Ті самі правила перевіряють і позиції з конфіга.
    /// </summary>
    public static class VillageLayout
    {
        /// <summary>Крок сітки плану: координати центру будівлі кратні йому.</summary>
        public const double GridStep = 2;

        /// <summary>Найменша координата центру за кожною віссю: мур стоїть на 5, будівля — квадрат із півстороною до 6.</summary>
        public const double Min = 12;

        /// <summary>Найбільша координата центру за кожною віссю.</summary>
        public const double Max = 88;

        /// <summary>
        /// Мінімальна відстань між центрами будівель за будь-якою віссю. Клієнт малює будівлю
        /// квадратом із півстороною до 6, тож два силуети не перетинаються, коли центри рознесені на 12.
        /// </summary>
        public const double MinSpacing = 12;

        /// <summary>Чи стоїть центр у межах муру й на вузлі сітки.</summary>
        public static bool IsValidSpot(double x, double y) => OnAxis(x) && OnAxis(y);

        /// <summary>Чи налазять дві будівлі з такими центрами одна на одну.</summary>
        public static bool Overlap(double x1, double y1, double x2, double y2)
            => Math.Max(Math.Abs(x1 - x2), Math.Abs(y1 - y2)) < MinSpacing;

        private static bool OnAxis(double value)
            => value >= Min && value <= Max && Math.Abs(Math.IEEERemainder(value, GridStep)) < 1e-9;
    }
}
