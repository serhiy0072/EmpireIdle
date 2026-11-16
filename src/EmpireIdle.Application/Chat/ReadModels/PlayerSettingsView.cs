using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Chat.ReadModels
{
    /// <summary>Налаштування гравця: мова інтерфейсу й мови, з яких можна обрати.</summary>
    public record PlayerSettingsView(string Language, IReadOnlyList<string> Languages);
}
