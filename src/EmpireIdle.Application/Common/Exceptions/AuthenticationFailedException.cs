namespace EmpireIdle.Application.Common.Exceptions
{
    /// <summary>
    /// Автентифікація не пройшла: невірні дані входу або непридатний refresh token.
    /// Мапиться на 401 — на відміну від 403, який означає «ми знаємо хто ти, але не можна».
    /// Повідомлення навмисно не розрізняє «немає такого email» і «невірний пароль»:
    /// інакше ендпоінт стає засобом перевірки, чи зареєстрована адреса.
    /// </summary>
    public sealed class AuthenticationFailedException : Exception
    {
        public AuthenticationFailedException(string message) : base(message) { }
    }
}
