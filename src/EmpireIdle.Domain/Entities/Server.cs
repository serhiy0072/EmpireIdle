using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Ігровий світ. Рівень визначає геометрію карти, стелю рівня будівель,
    /// доступні типи монстрів і рівні зброї — усе, що має відкриватись
    /// для всіх гравців одночасно, а не для тих, хто швидше клікає.
    ///
    /// Ключ int, не Guid як у решти сутностей: ServerId уже int у кожній
    /// таблиці й у кожному query-фільтрі, і зміна типу переписала б
    /// десяток міграцій заради однорідності, якої ніхто не побачить.
    ///
    /// Поза query-фільтром навмисно: фільтр «сервер поточного сервера»
    /// був би циклічним — саме звідси контекст і береться.
    /// </summary>
    public class Server
    {
        public int Id { get; private set; }

        public string Name { get; private set; } = null!;

        /// <summary>Рівень світу. Росте з часом і стелі не має (GDD §2.7).</summary>
        public int Level { get; private set; }

        public ServerState State { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Коли рівень підвищувався востаннє — від цього моменту відлічується наступний.
        /// </summary>
        public DateTime? LevelRaisedAt { get; private set; }

        /// <summary>Відколи світ на нинішньому рівні: світ, що ще не піднімався, — від створення.</summary>
        public DateTime LevelSince => LevelRaisedAt ?? CreatedAt;

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin).</summary>
        public uint Version { get; private set; }

        public Server(int id, string name, DateTime utcNow)
        {
            Id = id;
            Name = name;
            Level = 1;
            State = ServerState.Active;
            CreatedAt = utcNow;
            UpdatedAt = utcNow;
        }

        protected Server() { } // для EF Core

        /// <summary>
        /// Підвищує рівень світу на один. Стелі немає: новий рівень відкриває контент (GDD §2.7).
        ///
        /// Закритий світ теж розвивається: закриття реєстрації означає
        /// «новачків не беремо», а не «зупинилися».
        /// </summary>
        /// <exception cref="InvalidStateException">Світ згортається.</exception>
        public void RaiseLevel(DateTime utcNow)
        {
            if (State is ServerState.Sunset or ServerState.Archived)
                throw new InvalidStateException($"Server {Id} is {State} and does not evolve.");

            Level++;
            LevelRaisedAt = utcNow;
            UpdatedAt = utcNow;
        }

        /// <summary>
        /// Закриває реєстрацію. Викликається, коли світ дійшов стелі й заповнився:
        /// розсовувати межі більше нікуди, і новачків має приймати новий сервер.
        /// </summary>
        public void CloseRegistration(DateTime utcNow)
        {
            if (State != ServerState.Active)
                return;

            State = ServerState.Closed;
            UpdatedAt = utcNow;
        }

        /// <summary>Чи приймає світ нових гравців.</summary>
        public bool AcceptsNewPlayers => State == ServerState.Active;
    }
}
