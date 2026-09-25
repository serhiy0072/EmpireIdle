using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.ReadModels;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Territory.Queries
{
    /// <summary>Територія клану гравця. null — гравець поза кланом.</summary>
    public record GetClanTerritoryQuery(Guid PlayerId) : IRequest<ClanTerritoryView?>, IPlayerScopedRequest;

    public sealed class GetClanTerritoryQueryHandler : IRequestHandler<GetClanTerritoryQuery, ClanTerritoryView?>
    {
        private const int TopContributors = 10;

        private readonly IClanRepository _clanRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanQuestRepository _clanQuestRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly ClanTerritoryRules _rules;
        private readonly GameCatalog _catalog;

        public GetClanTerritoryQueryHandler(
            IClanRepository clanRepository,
            IClanStructureRepository structureRepository,
            IClanQuestRepository clanQuestRepository,
            IGarrisonRepository garrisonRepository,
            IPlayerRepository playerRepository,
            ClanTerritoryRules rules,
            GameCatalog catalog)
        {
            _clanRepository = clanRepository;
            _structureRepository = structureRepository;
            _clanQuestRepository = clanQuestRepository;
            _garrisonRepository = garrisonRepository;
            _playerRepository = playerRepository;
            _rules = rules;
            _catalog = catalog;
        }

        public async Task<ClanTerritoryView?> Handle(GetClanTerritoryQuery request, CancellationToken cancellationToken)
        {
            var clan = await _clanRepository.GetByMemberAsync(request.PlayerId, cancellationToken);

            if (clan is null)
                return null;

            var config = _catalog.Config.Clan.Territory;
            var progress = (await _clanQuestRepository.GetByClanAsync(clan.Id, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            var completed = progress.Values.Where(p => p.State != QuestState.InProgress).Select(p => p.QuestKey).ToList();

            var unlocks = config.SlotUnlocks
                .Select(u => new ClanSlotUnlockView(u.MinMembers, u.QuestKey,
                    (u.MinMembers is { } min && clan.Members.Count >= min) || (u.QuestKey is { } key && completed.Contains(key))))
                .ToList();

            var structures = new List<ClanStructureView>();

            foreach (var structure in await _structureRepository.GetByClanAsync(clan.Id, cancellationToken))
            {
                var garrison = await _garrisonRepository.GetByIdAsync(structure.GarrisonId, cancellationToken);

                structures.Add(new ClanStructureView(structure.Id, structure.X, structure.Y, structure.CompletesAt,
                    structure.AcceleratedShare,
                    garrison?.ReinforcementCount ?? 0,
                    garrison?.Reinforcements.Where(r => r.OwnerPlayerId == request.PlayerId).Sum(r => r.Count) ?? 0));
            }

            var slotQuests = config.SlotUnlocks.Where(u => u.QuestKey is not null).Select(u => u.QuestKey!).ToHashSet();

            var quests = _catalog.Config.Quests
                .Where(q => q.Scope == QuestScope.Clan)
                .Select(q =>
                {
                    var objective = q.Objectives[0];
                    var p = progress.GetValueOrDefault(q.Key);

                    return new ClanQuestView(q.Key, q.DisplayName, objective.Type, objective.Target,
                        p?.Amount ?? 0, objective.Count, p is not null && p.State != QuestState.InProgress,
                        q.ClanPoints, slotQuests.Contains(q.Key));
                })
                .ToList();

            var top = clan.Members
                .Where(m => m.Contribution > 0)
                .OrderByDescending(m => m.Contribution)
                .Take(TopContributors)
                .ToList();

            var names = top.Count == 0
                ? []
                : await _playerRepository.GetNamesAsync(top.Select(m => m.PlayerId).ToList(), cancellationToken);

            var role = clan.RoleOf(request.PlayerId);

            return new ClanTerritoryView(
                _rules.Enabled,
                clan.ContributionPoints,
                _rules.StructureCost,
                _rules.Radius,
                config.BuildMinutes,
                _rules.GarrisonCapacity,
                _rules.SlotsFor(clan.Members.Count, completed),
                config.MaxStructures,
                role is not null && role.Can(ClanPermission.BuildStructures),
                unlocks,
                structures,
                quests,
                top.Select(m => new ClanContributorView(m.PlayerId, names.GetValueOrDefault(m.PlayerId, "?"), m.Contribution)).ToList());
        }
    }
}
