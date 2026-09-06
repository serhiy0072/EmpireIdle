namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Конфігурація одного ресурсу.</summary>
    public class ResourceConfig
    {
        /// <summary>Унікальний ключ ресурсу (наприклад "gold").</summary>
        public string Key { get; set; } = null!;

        /// <summary>Відображувана назва.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>Іконка для фронтенду.</summary>
        public string Icon { get; set; } = null!;
    }

    /// <summary>Одна складова вартості — скільки якого ресурсу.</summary>
    public class ResourceCost
    {
        /// <summary>Ключ ресурсу.</summary>
        public string Resource { get; set; } = null!;

        /// <summary>Кількість.</summary>
        public int Amount { get; set; }
    }
}
