namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Хто кого покликав. Дві сторони одного механізму: заявку подає
    /// гравець і приймає офіцер, запрошення надсилає офіцер і приймає
    /// гравець. Життєвий цикл спільний, тому й сутність одна.
    /// </summary>
    public enum ClanRequestKind
    {
        Application = 0,
        Invite = 1
    }
}
