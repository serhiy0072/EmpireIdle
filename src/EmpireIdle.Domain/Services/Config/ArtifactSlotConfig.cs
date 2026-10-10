namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Тип артефактного слота героя: намисто, корона, кільце, пояс.
    ///
    /// Конфіг, а не enum: набір слотів — рішення балансу, і його зміна
    /// не має ставати міграцією. Номер слота на герої — позиція в списку.
    /// Тип не змінює базу предмета — лише бонуси заточки (GDD §9.12).
    /// </summary>
    public class ArtifactSlotConfig
    {
        /// <summary>Ключ типу; на нього посилається ItemConfig.ArtifactSlot.</summary>
        public string Key { get; set; } = null!;

        /// <summary>Назва для гравця.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>
        /// Два сталі бонуси — перші два ранги заточки. Підсилюють базу самого предмета,
        /// тож це ключі базових статів: Attack, Defense, UnitAttack, UnitDefense.
        /// </summary>
        public List<string> FixedBonuses { get; set; } = new();

        /// <summary>
        /// Пул випадкових бонусів — третій і четвертий ранги. Два різні стати без повтору,
        /// з вагами; діють на героя.
        /// </summary>
        public List<ArtifactPoolEntryConfig> RandomBonuses { get; set; } = new();
    }

    /// <summary>Стат у пулі слота і його вага при виборі.</summary>
    public class ArtifactPoolEntryConfig
    {
        public string Stat { get; set; } = null!;

        public double Weight { get; set; }
    }
}
