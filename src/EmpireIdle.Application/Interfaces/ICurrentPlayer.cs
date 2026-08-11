
namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Гравець, від імені якого виконується поточний запит.</summary>
    public interface ICurrentPlayer
    {
        /// <summary>Id гравця з токена; null — запит без автентифікації (фонові джоби).</summary>
        Guid? PlayerId { get; }

        /// <summary>Ідентифікатор акаунта (IdentityUser.Id) з токена. Null поза HTTP-контекстом.</summary>
        string? UserId { get; }
    }
}
