namespace EmpireIdle.API.DTOs;

/// <summary>
/// Пряме створення гравця. Ендпоінта немає: гравець з'являється
/// в межах реєстрації, однією транзакцією з користувачем.
/// </summary>
public record CreatePlayerRequest(string Username, string Email);

/// <summary>Картка гравця. Ендпоінта поки немає.</summary>
public record PlayerResponse(Guid PlayerId);
