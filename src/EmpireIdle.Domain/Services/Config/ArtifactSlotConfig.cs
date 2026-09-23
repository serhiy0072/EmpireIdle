namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Тип артефактного слота героя: намисто, корона, кільце, пояс.
    ///
    /// Конфіг, а не enum: набір слотів — рішення балансу, і його зміна
    /// не має ставати міграцією. Номер слота на герої — позиція в списку.
    /// </summary>
    public class ArtifactSlotConfig
    {
        /// <summary>Ключ типу; на нього посилається ItemConfig.ArtifactSlot.</summary>
        public string Key { get; set; } = null!;

        /// <summary>Назва для гравця.</summary>
        public string DisplayName { get; set; } = null!;
    }
}
