namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Світ поточного scope. З токена — для HTTP, явно — для фонових джобів.</summary>
    public interface IServerContext
    {
        /// <summary>Id сервера з claim `serverId`.</summary>
        int ServerId { get; }

        /// <summary>
        /// Встановлює світ явно — для фонових прогонів і реєстрації,
        /// де токена немає. Діє в межах одного DI-scope.
        /// </summary>
        void UseServer(int serverId);
    }
}
