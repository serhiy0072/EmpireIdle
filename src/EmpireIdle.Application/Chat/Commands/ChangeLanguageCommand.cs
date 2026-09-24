using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Chat.Commands
{
    /// <summary>
    /// Змінити мову інтерфейсу гравця. Від неї залежать назви в каталозі
    /// й переклади в чаті; нові повідомлення гравця підписуються нею.
    /// </summary>
    public record ChangeLanguageCommand(Guid PlayerId, string Language)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class ChangeLanguageCommandHandler : IRequestHandler<ChangeLanguageCommand>
    {
        private readonly IPlayerRepository _players;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;

        public ChangeLanguageCommandHandler(IPlayerRepository players, GameCatalog catalog, IUnitOfWork unitOfWork)
        {
            _players = players;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(ChangeLanguageCommand request, CancellationToken cancellationToken)
        {
            // Клієнт пропонує лише мови з каталогу: інша — це баг клієнта, не вибір гравця
            if (!_catalog.Config.Localization.SupportedLanguages.Contains(request.Language))
                throw new RequirementNotMetException($"Language '{request.Language}' is not supported.");

            var player = await _players.GetByIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Player", request.PlayerId.ToString());

            player.ChangeLanguage(request.Language);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
