namespace EmpireIdle.Domain.Enums
{
    /// <summary>Канал чату (GDD §7.3).</summary>
    public enum ChatChannel
    {
        /// <summary>Усі гравці світу.</summary>
        Server = 1,

        /// <summary>Члени одного клану.</summary>
        Clan = 2,

        /// <summary>Двоє гравців одного світу.</summary>
        Private = 3
    }
}
