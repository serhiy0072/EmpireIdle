namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Тип особистого листа (GDD §7.4). Лист тримає лише тип і ReferenceId:
    /// актуальний стан того, на що він посилається, читається при відкритті.
    /// </summary>
    public enum MailKind
    {
        /// <summary>Запрошення в клан; ReferenceId — ClanRequest.</summary>
        ClanInvite = 1,

        /// <summary>Падіння міста; ReferenceId — VillageFall.</summary>
        CityFall = 2
    }
}
