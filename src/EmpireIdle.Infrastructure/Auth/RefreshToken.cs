using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireIdle.Infrastructure.Auth
{
    /// <summary>
    /// Refresh token для оновлення JWT без повторного логіну.
    /// Зберігається в БД, одноразовий (ротація при кожному використанні).
    /// </summary>
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = null!;
        public string Token { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? ReplacedByToken { get; set; }

        public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
    }
}
