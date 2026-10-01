namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Облікові записи входу (Identity). Застосунку потрібне лише створення акаунта
    /// під нового гравця — логін і токени лишаються в інфраструктурі.
    /// </summary>
    public interface IUserAccounts
    {
        /// <summary>
        /// Створює акаунт у поточній транзакції. Відмова (слабкий пароль, зайнятий email) —
        /// RequirementNotMetException з причиною.
        /// </summary>
        /// <returns>Id користувача Identity.</returns>
        Task<string> CreateAsync(string userName, string email, string password, CancellationToken cancellationToken = default);
    }
}
