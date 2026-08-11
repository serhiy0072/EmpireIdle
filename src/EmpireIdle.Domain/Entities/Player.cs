namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Гравець — власник села і гаманця.
    /// </summary>
    public class Player : Entity
    {
        /// <summary>Ім'я користувача.</summary>
        public string Username { get; private set; } = null!;

        /// <summary>Email адреса.</summary>
        public string Email { get; private set; } = null!;

        /// <summary>Ідентифікатор ігрового сервера, на якому живе цей гравець.</summary>
        public int ServerId { get; private set; }

        /// <summary>Дата реєстрації.</summary>
        public DateTime CreatedAt { get; private set; }

        public Player(Guid id, string username, string email, int serverId = 1) : base(id)
        {
            Username = username;
            Email = email;
            CreatedAt = DateTime.UtcNow;
            ServerId = serverId;
        }

        protected Player() { } // для EF Core
    }
}
