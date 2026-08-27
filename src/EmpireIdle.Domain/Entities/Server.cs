using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>Стан життєвого циклу світу.</summary>
    public enum ServerState
    {
        /// <summary>Створений, реєстрація відкрита.</summary>
        Active = 0,

        /// <summary>Досяг стелі й заповнений — реєстрація закрита, гра триває.</summary>
        Closed = 1,

        /// <summary>Оголошено закриття, зворотний відлік до архівації.</summary>
        Sunset = 2,

        /// <summary>Гра зупинена, дані збережені.</summary>
        Archived = 3
    }

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

        /// <summary>Рівень світу. Росте від зрілості або від перенаселення.</summary>
        public int Level { get; private set; }

        public ServerState State { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Коли рівень підвищувався востаннє. Потрібне для мінімального
        /// інтервалу між підйомами: без нього обидва тригери могли б
        /// спрацювати поспіль і перескочити тір.
        /// </summary>
        public DateTime? LevelRaisedAt { get; private set; }

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
        /// Підвищує рівень світу на один.
        /// </summary>
        /// <param name="maxLevel">Стеля з конфіга карти.</param>
        /// <exception cref="InvalidStateException">Світ уже на стелі або не активний.</exception>
        public void RaiseLevel(int maxLevel, DateTime utcNow)
        {
            if (State != ServerState.Active)
                throw new InvalidStateException($"Server {Id} is {State} and does not evolve.");

            if (Level >= maxLevel)
                throw new InvalidStateException($"Server {Id} is already at the maximum level {maxLevel}.");

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
