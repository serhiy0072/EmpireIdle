using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>Повідомлення чату. RecipientId — лише для приватного каналу.</summary>
public record SendChatMessageRequest(ChatChannel Channel, Guid? RecipientId, string Text);

public record ChatMessageSentResponse(Guid MessageId);

/// <summary>Мова інтерфейсу — код ISO 639-1 зі списку PlayerSettingsView.Languages.</summary>
public record ChangeLanguageRequest(string Language);
