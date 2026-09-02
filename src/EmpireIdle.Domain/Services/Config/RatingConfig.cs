namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Ваги рейтингу. Кожен компонент нормалізується до 0..1 за своїм
    /// орієнтиром і множиться на вагу — тому вага прямо означає «яку частку
    /// рейтингу може дати ця вісь».
    ///
    /// Power домінує, але має стелю: армія обмежена казармами, герої слотами.
    /// Хто вперся в неї, змагається вкладеннями — і саме тому нормалізація
    /// обрізає, а не масштабує нескінченно.
    /// </summary>
    public class RatingConfig
    {
        /// <summary>Частка рейтингу, яку може дати бойова сила.</summary>
        public double PowerWeight { get; set; } = 0.55;

        /// <summary>Частка за розвиток селища — сума рівнів будівель.</summary>
        public double DevelopmentWeight { get; set; } = 0.25;

        /// <summary>Частка за активність — бої, монстри, квести, внески.</summary>
        public double ActivityWeight { get; set; } = 0.20;

        /// <summary>Сила, на якій PowerWeight вичерпується повністю.</summary>
        public double PowerReference { get; set; } = 50_000;

        /// <summary>Сума рівнів будівель, на якій вичерпується DevelopmentWeight.</summary>
        public int DevelopmentReference { get; set; } = 300;

        /// <summary>Очки активності, на яких вичерпується ActivityWeight.</summary>
        public int ActivityReference { get; set; } = 5_000;

        /// <summary>Множник підсумку — щоб рейтинг був цілим числом, а не часткою.</summary>
        public int Scale { get; set; } = 10_000;

        /// <summary>Очок активності за вбитого монстра.</summary>
        public int PointsPerMonster { get; set; } = 5;

        /// <summary>Очок за виграний бій.</summary>
        public int PointsPerBattleWon { get; set; } = 10;

        /// <summary>Очок за забраний квест.</summary>
        public int PointsPerQuest { get; set; } = 20;

        /// <summary>Очок за одиницю внеску в серверний квест.</summary>
        public int PointsPerContribution { get; set; } = 1;
    }
}
