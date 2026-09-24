using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>Оголошення для dev-публікації. ExpiresAt null — строк за замовчуванням.</summary>
public record PublishAnnouncementRequest(AnnouncementKind Kind, string Title, string Body, DateTime? ExpiresAt);
