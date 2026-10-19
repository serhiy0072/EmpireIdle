namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Провізія села (рішення 20.10.2026): Їдальня щогодини з'їдає м'ясо — тим більше, що більше селище
    /// (сума рівнів будівель із рівнями). Не вистачило — село голодує: видобуток і захист знижені.
    /// Ключ м'яса — "food": назва змінилась, ідентифікатор лишився, щоб не мігрувати дані заради слова.
    /// </summary>
    public class ProvisionsConfig
    {
        /// <summary>Будівля, що їсть м'ясо; null — у світі немає Їдальні, і село не голодує.</summary>
        public string? MessHallBuildingKey { get; set; }

        /// <summary>Ресурс, який їсть село.</summary>
        public string MealResource { get; set; } = "food";

        /// <summary>М'яса за годину за кожен рівень будівель із рівнями.</summary>
        public int MealPerBuildingLevelPerHour { get; set; }

        /// <summary>Множник видобутку всіх ресурсів, крім м'яса, поки село голодує.</summary>
        public double HungerProductionMultiplier { get; set; } = 1.0;

        /// <summary>Множник захисту голодного села.</summary>
        public double HungerDefenceMultiplier { get; set; } = 1.0;
    }
}
