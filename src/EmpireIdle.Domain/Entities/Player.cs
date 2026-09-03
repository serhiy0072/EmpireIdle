namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Гравець — власник села і гаманця.
    /// </summary>
    public class Player : Entity
    {
        /// <summary>
        /// Акаунт-власник (IdentityUser.Id). Один акаунт може мати гравців на різних серверах,
        /// але гаманець у них спільний — саме за цим полем його й шукають.
        /// </summary>
        public string UserId { get; private set; } = null!;

        /// <summary>Ім'я користувача.</summary>
        public string Username { get; private set; } = null!;

        /// <summary>Email адреса.</summary>
        public string Email { get; private set; } = null!;

        /// <summary>Ідентифікатор ігрового сервера, на якому живе цей гравець.</summary>
        public int ServerId { get; private set; }

        /// <summary>Дата реєстрації.</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Клан гравця; null — без клану.
        ///
        /// Денормалізація: істина живе в Clan.Members, а це поле дає перевірку
        /// «чи ми в одному клані» без завантаження всього складу. Оновлюється
        /// разом із членством, у тій самій транзакції.
        /// </summary>
        public Guid? ClanId { get; private set; }

        public Player(Guid id, string username, string email, string userId, DateTime utcNow, int serverId = 1) : base(id)
        {
            UserId = userId;
            Username = username;
            Email = email;
            CreatedAt = utcNow;
            ServerId = serverId;
        }

        protected Player() { } // для EF Core

        /// <summary>Прив'язує гравця до клану. Істина — у складі клану; це дзеркало.</summary>
        public void JoinClan(Guid clanId) => ClanId = clanId;

        public void LeaveClan() => ClanId = null;
    }
}
