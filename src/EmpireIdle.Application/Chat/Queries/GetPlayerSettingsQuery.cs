using EmpireIdle.Application.Common;
using EmpireIdle.Application.Chat.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Chat.Queries
{
    public record GetPlayerSettingsQuery(Guid PlayerId) : IRequest<PlayerSettingsView>, IPlayerScopedRequest;

    public sealed class GetPlayerSettingsQueryHandler : IRequestHandler<GetPlayerSettingsQuery, PlayerSettingsView>
    {
        private readonly IPlayerRepository _players;
        private readonly GameCatalog _catalog;

        public GetPlayerSettingsQueryHandler(IPlayerRepository players, GameCatalog catalog)
        {
            _players = players;
            _catalog = catalog;
        }

        public async Task<PlayerSettingsView> Handle(GetPlayerSettingsQuery request, CancellationToken cancellationToken)
        {
            var player = await _players.RequireAsync(request.PlayerId, cancellationToken);

            return new PlayerSettingsView(player.Language, _catalog.Config.Localization.SupportedLanguages);
        }
    }
}
