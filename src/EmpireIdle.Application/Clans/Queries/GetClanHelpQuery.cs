using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;

namespace EmpireIdle.Application.Clans.Queries
{
    /// <summary>Список допомоги клану — порожній, якщо гравець без клану.</summary>
    public record GetClanHelpQuery(Guid PlayerId) : IRequest<List<ClanHelpItem>>, IPlayerScopedRequest;

    /// <summary>Обробник GetClanHelpQuery.</summary>
    public sealed class GetClanHelpQueryHandler : IRequestHandler<GetClanHelpQuery, List<ClanHelpItem>>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IClanHelpRepository _helpRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IPlayerResearchRepository _researchRepository;
        private readonly ClanBonuses _bonuses;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetClanHelpQueryHandler(
            IClanRepository clanRepository,
            IClanHelpRepository helpRepository,
            IPlayerRepository playerRepository,
            IPlayerResearchRepository researchRepository,
            ClanBonuses bonuses,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _clanRepository = clanRepository;
            _helpRepository = helpRepository;
            _playerRepository = playerRepository;
            _researchRepository = researchRepository;
            _bonuses = bonuses;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<List<ClanHelpItem>> Handle(GetClanHelpQuery request, CancellationToken cancellationToken)
        {
            // Потрібен лише id клану — склад тут не читається
            var clanId = await _clanRepository.GetClanIdByMemberAsync(request.PlayerId, cancellationToken);

            if (clanId is null)
                return [];

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var requests = await _helpRepository.GetActiveByClanAsync(clanId.Value, now, cancellationToken);

            if (requests.Count == 0)
                return [];

            var helpCap = (await _bonuses.ForClanAsync(clanId.Value, cancellationToken)).Of(TechnologyStat.HelpShareCap);

            // У досліджень свій ліміт допомог (рішення 28.10.2026)
            int MaxHelpers(ClanHelpTarget target) => target == ClanHelpTarget.Research
                ? _catalog.Config.Technologies.Personal.MaxHelps
                : _catalog.Config.Clan.MaxHelpersWith(helpCap);

            var names = await _playerRepository.GetNamesAsync(
                requests.Select(r => r.PlayerId).Distinct().ToList(), cancellationToken);

            // Соклановець має бачити, яку саме технологію прискорює, а не просто «дослідження»
            var researchIds = requests
                .Where(r => r.TargetType == ClanHelpTarget.Research)
                .Select(r => r.TargetId)
                .ToList();
            var researchKeys = researchIds.Count == 0
                ? []
                : await _researchRepository.GetActiveKeysAsync(researchIds, cancellationToken);

            return requests
                .Select(r => new ClanHelpItem(
                    r.Id,
                    r.PlayerId,
                    names.GetValueOrDefault(r.PlayerId, "—"),
                    r.TargetType,
                    r.TargetId,
                    r.HelpCount,
                    MaxHelpers(r.TargetType),
                    r.Helpers.Any(h => h.HelperId == request.PlayerId),
                    r.PlayerId == request.PlayerId,
                    r.CreatedAt,
                    r.ExpiresAt,
                    r.TargetType == ClanHelpTarget.Research ? researchKeys.GetValueOrDefault(r.TargetId) : null))
                .ToList();
        }
    }
}
