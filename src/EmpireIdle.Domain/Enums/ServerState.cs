namespace EmpireIdle.Domain.Enums
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
}
